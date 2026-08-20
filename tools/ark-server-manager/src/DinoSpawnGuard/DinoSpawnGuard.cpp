#include <API/ARK/Ark.h>
#include <json.hpp>

#include <Windows.h>
#include <algorithm>
#include <cctype>
#include <cmath>
#include <cstdint>
#include <fstream>
#include <iomanip>
#include <sstream>
#include <string>
#include <vector>

#pragma comment(lib, "ArkApi.lib")

namespace
{
    constexpr const char* PluginVersion = "1.0";

    struct Settings
    {
        bool enabled = true;
        bool dryRun = false;
        double delayAfterSaveSeconds = 3.0;
        int dinosPerTick = 50;
        double maxRelocationDistance = 2500.0;
        std::vector<std::string> majorDinoClasses;
    } settings;

    HMODULE pluginModule = nullptr;
    UWorld* observedWorld = nullptr;
    long double lastObservedSaveTime = -1.0;
    bool scanQueued = false;
    bool scanInProgress = false;
    long double scanNotBefore = 0.0;
    std::vector<APrimalDinoCharacter*> candidates;
    std::size_t nextCandidate = 0;
    unsigned long long generation = 0;
    unsigned long long totalChecked = 0;
    unsigned long long totalBlocked = 0;
    unsigned long long totalRelocated = 0;
    unsigned long long totalFailed = 0;
    unsigned long long lastChecked = 0;
    unsigned long long lastBlocked = 0;
    unsigned long long lastRelocated = 0;
    unsigned long long lastFailed = 0;
    std::string lastError;

    const std::vector<std::string> DefaultMajorDinoClasses =
    {
        "rex_character", "spino_character", "gigant_character", "carcha_character",
        "yutyrannus_character", "allo_character", "therizino_character",
        "megatherium_character", "mammoth_character", "rhino_character",
        "direbear_character", "thylacoleo_character", "lionfishlion_character",
        "andrewsarchus_character", "crab_character", "rockdrake_character",
        "cherufe_character", "deinonychus_character", "baryonyx_character",
        "carno_character", "trike_character", "stego_character", "sauropod_character",
        "paracer_character", "ankylo_character", "doed_character",
        "beaver_character", "daeodon_character"
    };

    std::string Lower(std::string value)
    {
        std::transform(value.begin(), value.end(), value.begin(),
            [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
        return value;
    }

    std::string PluginDirectory()
    {
        char path[MAX_PATH] = {};
        if (!pluginModule || GetModuleFileNameA(pluginModule, path, MAX_PATH) == 0) return "";
        std::string result(path);
        const std::size_t separator = result.find_last_of("\\/");
        return separator == std::string::npos ? "" : result.substr(0, separator);
    }

    void LoadSettings()
    {
        settings.majorDinoClasses = DefaultMajorDinoClasses;
        const std::string path = PluginDirectory() + "\\config.json";
        std::ifstream file(path);
        if (!file)
        {
            Log::GetLog()->warn("config.json was not found; safe built-in defaults are active");
            return;
        }
        try
        {
            nlohmann::json config;
            file >> config;
            settings.enabled = config.value("Enabled", settings.enabled);
            settings.dryRun = config.value("DryRun", settings.dryRun);
            settings.delayAfterSaveSeconds = std::max(1.0, config.value("DelayAfterSaveSeconds", settings.delayAfterSaveSeconds));
            settings.dinosPerTick = std::max(1, std::min(200, config.value("DinosPerTick", settings.dinosPerTick)));
            settings.maxRelocationDistance = std::max(300.0, std::min(5000.0,
                config.value("MaxRelocationDistance", settings.maxRelocationDistance)));
            if (config.contains("MajorDinoClasses") && config["MajorDinoClasses"].is_array())
            {
                std::vector<std::string> configured;
                for (const auto& entry : config["MajorDinoClasses"])
                    if (entry.is_string() && !entry.get<std::string>().empty()) configured.push_back(Lower(entry.get<std::string>()));
                if (!configured.empty()) settings.majorDinoClasses = configured;
            }
        }
        catch (const std::exception& error)
        {
            lastError = error.what();
            Log::GetLog()->error("config.json could not be read: {}. Built-in defaults are active", error.what());
        }
    }

    void SendRconReply(RCONClientConnection* connection, int packetId, const FString& message)
    {
        FString reply = message + "\n";
        connection->SendMessageW(packetId, 0, &reply);
    }

    std::string DinoClassName(APrimalDinoCharacter* dino)
    {
        if (!dino || !dino->ClassField()) return "";
        FString fullName;
        dino->ClassField()->GetFullName(&fullName, nullptr);
        return Lower(ArkApi::Tools::Utf8Encode(std::wstring(*fullName)));
    }

    bool IsMajorClass(const std::string& className)
    {
        if (className.find("boss") != std::string::npos ||
            className.find("megarex_character") != std::string::npos ||
            className.find("megacarno_character") != std::string::npos ||
            className.find("megacrab_character") != std::string::npos)
            return false;
        for (const std::string& pattern : settings.majorDinoClasses)
            if (className.find(pattern) != std::string::npos) return true;
        return false;
    }

    bool IsValidWildCandidate(APrimalDinoCharacter* dino)
    {
        if (!dino || !dino->IsValidLowLevelFast(false) || dino->IsPendingKillPending() || !dino->IsAlive()) return false;
        if (dino->BPIsTamed() || dino->TargetingTeamField() >= 50000) return false;
        USceneComponent* root = dino->RootComponentField();
        if (!root || root->AttachParentField() != nullptr) return false;
        return IsMajorClass(DinoClassName(dino));
    }

    bool Finite(const FVector& value)
    {
        return std::isfinite(value.X) && std::isfinite(value.Y) && std::isfinite(value.Z);
    }

    double DistanceSquared(const FVector& left, const FVector& right)
    {
        const double dx = static_cast<double>(left.X) - right.X;
        const double dy = static_cast<double>(left.Y) - right.Y;
        const double dz = static_cast<double>(left.Z) - right.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    bool FindVerifiedSafeLocation(UWorld* world, APrimalDinoCharacter* dino,
        const FVector& original, const FRotator& rotation, const FVector& proposedAdjustment, FVector& result)
    {
        if (!world || !dino) return false;
        const double maxDistanceSquared = settings.maxRelocationDistance * settings.maxRelocationDistance;

        auto verify = [&](FVector candidate)
        {
            if (!Finite(candidate) || DistanceSquared(candidate, original) > maxDistanceSquared) return false;
            FVector traceFrom = original;
            if (!world->FindTeleportSpot(dino, &candidate, rotation, &traceFrom)) return false;
            if (!Finite(candidate) || DistanceSquared(candidate, original) > maxDistanceSquared || DistanceSquared(candidate, original) < 625.0)
                return false;
            FVector adjustment(0, 0, 0);
            FVector trace = original;
            if (world->EncroachingBlockingGeometry(dino, candidate, rotation, &adjustment, &trace)) return false;
            result = candidate;
            return true;
        };

        if (Finite(proposedAdjustment))
        {
            FVector adjusted(original.X + proposedAdjustment.X, original.Y + proposedAdjustment.Y,
                original.Z + proposedAdjustment.Z + 50.0f);
            if (verify(adjusted)) return true;
        }

        static const float Directions[][2] =
        {
            { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 },
            { 0.7071f, 0.7071f }, { -0.7071f, 0.7071f },
            { 0.7071f, -0.7071f }, { -0.7071f, -0.7071f }
        };
        static const float Radii[] = { 300.0f, 600.0f, 1000.0f, 1500.0f, 2000.0f, 2500.0f };
        static const float Heights[] = { 100.0f, 300.0f, 600.0f };
        for (float radius : Radii)
        {
            if (radius > settings.maxRelocationDistance) break;
            for (const auto& direction : Directions)
                for (float height : Heights)
                {
                    FVector candidate(original.X + direction[0] * radius,
                        original.Y + direction[1] * radius, original.Z + height);
                    if (verify(candidate)) return true;
                }
        }
        return false;
    }

    void FinishScan()
    {
        scanInProgress = false;
        candidates.clear();
        nextCandidate = 0;
        Log::GetLog()->info("Generation {} complete: checked={}, blocked={}, relocated={}, failed={}, dry_run={}",
            generation, lastChecked, lastBlocked, lastRelocated, lastFailed, settings.dryRun);
    }

    void BeginScan(UWorld* world)
    {
        scanQueued = false;
        candidates.clear();
        nextCandidate = 0;
        lastChecked = lastBlocked = lastRelocated = lastFailed = 0;
        if (!world || !settings.enabled) return;

        TArray<AActor*> actors;
        UGameplayStatics::GetAllActorsOfClass(static_cast<UObject*>(world),
            TSubclassOf<AActor>(APrimalDinoCharacter::GetPrivateStaticClass()), &actors);
        for (AActor* actor : actors)
        {
            auto* dino = static_cast<APrimalDinoCharacter*>(actor);
            if (IsValidWildCandidate(dino)) candidates.push_back(dino);
        }
        ++generation;
        scanInProgress = !candidates.empty();
        Log::GetLog()->info("Generation {} started after save: active major wild candidates={}, dry_run={}",
            generation, candidates.size(), settings.dryRun);
        if (!scanInProgress) FinishScan();
    }

    void CheckDino(UWorld* world, APrimalDinoCharacter* dino)
    {
        if (!IsValidWildCandidate(dino)) return;
        USceneComponent* root = dino->RootComponentField();
        FVector original = root->RelativeLocationField();
        FRotator rotation = root->RelativeRotationField();
        if (!Finite(original)) return;

        ++lastChecked;
        ++totalChecked;
        FVector proposedAdjustment(0, 0, 0);
        FVector traceFrom = original;
        if (!world->EncroachingBlockingGeometry(dino, original, rotation, &proposedAdjustment, &traceFrom)) return;

        ++lastBlocked;
        ++totalBlocked;
        const std::string className = DinoClassName(dino);
        if (settings.dryRun)
        {
            Log::GetLog()->warn("Dry-run blocked dino: class={}, x={}, y={}, z={}",
                className, original.X, original.Y, original.Z);
            return;
        }

        FVector safeLocation;
        if (!FindVerifiedSafeLocation(world, dino, original, rotation, proposedAdjustment, safeLocation))
        {
            ++lastFailed;
            ++totalFailed;
            Log::GetLog()->error("No safe relocation found: class={}, x={}, y={}, z={}",
                className, original.X, original.Y, original.Z);
            return;
        }

        FVector destination = safeLocation;
        FRotator destinationRotation = rotation;
        if (dino->TeleportTo(&destination, &destinationRotation, false, false))
        {
            dino->ForceNetUpdate(false, true, false);
            ++lastRelocated;
            ++totalRelocated;
            Log::GetLog()->warn("Relocated blocked wild dino: class={}, from=({},{},{}), to=({},{},{})",
                className, original.X, original.Y, original.Z,
                destination.X, destination.Y, destination.Z);
        }
        else
        {
            ++lastFailed;
            ++totalFailed;
            Log::GetLog()->error("Teleport was rejected: class={}, x={}, y={}, z={}",
                className, original.X, original.Y, original.Z);
        }
    }

    void QueueScan(UWorld* world, const char* reason)
    {
        if (!world || !settings.enabled) return;
        scanQueued = true;
        scanNotBefore = world->TimeSecondsField() + settings.delayAfterSaveSeconds;
        Log::GetLog()->info("Scan queued: reason={}, starts_in={}s", reason, settings.delayAfterSaveSeconds);
    }

    void Tick()
    {
        if (ArkApi::GetApiUtils().GetStatus() != ArkApi::ServerStatus::Ready) return;
        UWorld* world = ArkApi::GetApiUtils().GetWorld();
        AShooterGameMode* gameMode = ArkApi::GetApiUtils().GetShooterGameMode();
        if (!world || !gameMode) return;

        if (world != observedWorld)
        {
            observedWorld = world;
            lastObservedSaveTime = gameMode->LastTimeSavedWorldField();
            scanQueued = scanInProgress = false;
            candidates.clear();
            nextCandidate = 0;
            Log::GetLog()->info("World baseline established; no startup scan will run");
            return;
        }

        const long double savedAt = gameMode->LastTimeSavedWorldField();
        if (savedAt > lastObservedSaveTime + 0.001L)
        {
            lastObservedSaveTime = savedAt;
            QueueScan(world, "save_completed");
        }

        if (!settings.enabled) return;
        if (scanQueued && !scanInProgress && world->TimeSecondsField() >= scanNotBefore) BeginScan(world);
        if (!scanInProgress) return;

        const int batch = std::max(1, settings.dinosPerTick);
        int processed = 0;
        while (nextCandidate < candidates.size() && processed < batch)
        {
            CheckDino(world, candidates[nextCandidate++]);
            ++processed;
        }
        if (nextCandidate >= candidates.size()) FinishScan();
    }

    FString BuildStatusReply()
    {
        std::ostringstream out;
        out << "OK=1\nPLUGIN_VERSION=" << PluginVersion
            << "\nENABLED=" << (settings.enabled ? 1 : 0)
            << "\nDRY_RUN=" << (settings.dryRun ? 1 : 0)
            << "\nMODE=AFTER_SAVE_ONLY"
            << "\nQUEUED=" << (scanQueued ? 1 : 0)
            << "\nIN_PROGRESS=" << (scanInProgress ? 1 : 0)
            << "\nGENERATION=" << generation
            << "\nCANDIDATES=" << candidates.size()
            << "\nNEXT_CANDIDATE=" << nextCandidate
            << "\nLAST_CHECKED=" << lastChecked
            << "\nLAST_BLOCKED=" << lastBlocked
            << "\nLAST_RELOCATED=" << lastRelocated
            << "\nLAST_FAILED=" << lastFailed
            << "\nTOTAL_CHECKED=" << totalChecked
            << "\nTOTAL_BLOCKED=" << totalBlocked
            << "\nTOTAL_RELOCATED=" << totalRelocated
            << "\nTOTAL_FAILED=" << totalFailed
            << "\nCLASS_PATTERN_COUNT=" << settings.majorDinoClasses.size()
            << "\nLAST_ERROR=" << lastError;
        return FString(out.str());
    }

    void StatusRcon(RCONClientConnection* connection, RCONPacket* packet, UWorld*)
    {
        SendRconReply(connection, packet->Id, BuildStatusReply());
    }

    void PauseRcon(RCONClientConnection* connection, RCONPacket* packet, UWorld*)
    {
        settings.enabled = false;
        scanQueued = scanInProgress = false;
        candidates.clear();
        nextCandidate = 0;
        Log::GetLog()->warn("Dino relocation paused by RCON");
        SendRconReply(connection, packet->Id, FString("OK=1\nENABLED=0\nMESSAGE=scan and relocation paused"));
    }

    void ResumeRcon(RCONClientConnection* connection, RCONPacket* packet, UWorld*)
    {
        settings.enabled = true;
        Log::GetLog()->warn("Dino relocation resumed; the next completed save will trigger a scan");
        SendRconReply(connection, packet->Id, FString("OK=1\nENABLED=1\nMESSAGE=next completed save will trigger a scan"));
    }

    void Load()
    {
        Log::Get().Init("DinoSpawnGuard");
        LoadSettings();
        ArkApi::GetCommands().AddRconCommand("DinoSpawnGuard.Status", &StatusRcon);
        ArkApi::GetCommands().AddRconCommand("DinoSpawnGuard.Pause", &PauseRcon);
        ArkApi::GetCommands().AddRconCommand("DinoSpawnGuard.Resume", &ResumeRcon);
        ArkApi::GetCommands().AddOnTimerCallback("DinoSpawnGuard.Tick", &Tick);
        Log::GetLog()->info("DinoSpawnGuard {} loaded: enabled={}, dry_run={}, classes={}, after-save only",
            PluginVersion, settings.enabled, settings.dryRun, settings.majorDinoClasses.size());
    }

    void Unload()
    {
        ArkApi::GetCommands().RemoveOnTimerCallback("DinoSpawnGuard.Tick");
        ArkApi::GetCommands().RemoveRconCommand("DinoSpawnGuard.Status");
        ArkApi::GetCommands().RemoveRconCommand("DinoSpawnGuard.Pause");
        ArkApi::GetCommands().RemoveRconCommand("DinoSpawnGuard.Resume");
    }
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH:
        pluginModule = module;
        Load();
        break;
    case DLL_PROCESS_DETACH:
        Unload();
        break;
    }
    return TRUE;
}
