#include <API/ARK/Ark.h>

#include <Windows.h>
#include <algorithm>
#include <cctype>
#include <cmath>
#include <cstdint>
#include <iomanip>
#include <map>
#include <sstream>
#include <string>
#include <unordered_set>
#include <vector>

#pragma comment(lib, "ArkApi.lib")

namespace
{
    enum ResourceMask
    {
        ResourceNone = 0,
        ResourceMetal = 1,
        ResourceCrystal = 2,
        ResourceObsidian = 4
    };

    struct ResourceSource
    {
        const char* id;
        const char* realm;
        double latitude;
        double longitude;
        double z;
        double searchRadiusGps;
    };

    struct ResourceSpot
    {
        std::string id;
        std::string sourceId;
        std::string realm;
        double latitudeTotal = 0;
        double longitudeTotal = 0;
        int expectedResources = ResourceNone;
        int metal = 0;
        int crystal = 0;
        int obsidian = 0;
        int totalFoliage = 0;
        double metalHealth = 0;
        double crystalHealth = 0;
        double obsidianHealth = 0;
    };

    // The broad source circles are used only to discover nodes in resource-rich
    // regions. Results are divided by the node's real position into much smaller
    // cells before they are returned to the manager.
    const std::vector<ResourceSource> Sources =
    {
        { "vardiland_snow", "MIDGARD", 76.0, 18.0, 0.0, 1.8 },
        { "dvergheim_mines", "MIDGARD", 88.5, 13.0, -10000.0, 1.5 },
        { "vannaland_north", "MIDGARD", 19.0, 35.0, 7000.0, 1.8 },
        { "vannaland_east", "MIDGARD", 24.0, 72.0, 7000.0, 1.8 },
        { "balheimr_volcano", "MIDGARD", 84.0, 82.0, 5000.0, 1.8 },
        { "space_cave", "MIDGARD", 86.0, 96.0, -10000.0, 1.5 },
        { "asgard_mountains", "ASGARD", 43.0, 48.0, -320000.0, 1.8 },
        { "jotunheim_ice", "JOTUNHEIM", 76.0, 42.0, -130000.0, 1.8 },
        { "vanaheim_crystal", "VANAHEIM", 16.0, 82.0, -150000.0, 1.8 }
    };

    // A cell's corner-to-corner diameter is about 0.50 GPS. That is roughly
    // 3,535 Unreal units, short enough for an Argentavis to cross in under
    // about three seconds while keeping neighbouring clusters separate.
    constexpr double SpotCellGps = 0.35;
    constexpr double SpotRadiusGps = 0.25;
    constexpr double GpsScale = 7140.0;
    constexpr int MinimumResourceRocks = 6;

    std::map<std::string, ResourceSpot> Spots;
    std::unordered_set<unsigned long long> seenFoliage;
    std::size_t nextSource = 0;
    unsigned long long completedGeneration = 0;
    long double completedAt = 0;
    unsigned long long scanFailures = 0;
    bool scanningEnabled = true;
    bool scanInProgress = false;
    std::string lastError;

    void SendRconReply(RCONClientConnection* connection, int packetId, const FString& message)
    {
        FString reply = message + "\n";
        connection->SendMessageW(packetId, 0, &reply);
    }

    std::string Lower(std::string value)
    {
        std::transform(value.begin(), value.end(), value.begin(),
            [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
        return value;
    }

    int ResourceMaskForClass(UClass* resourceClass)
    {
        if (!resourceClass) return ResourceNone;
        FString fullName;
        resourceClass->GetDefaultObject(true)->GetFullName(&fullName, nullptr);
        const std::string name = Lower(ArkApi::Tools::Utf8Encode(std::wstring(*fullName)));
        int mask = ResourceNone;
        if (name.find("primalitemresource_metal_c") != std::string::npos &&
            name.find("metalingot") == std::string::npos && name.find("scrapmetal") == std::string::npos)
            mask |= ResourceMetal;
        if (name.find("primalitemresource_crystal_c") != std::string::npos)
            mask |= ResourceCrystal;
        if (name.find("primalitemresource_obsidian_c") != std::string::npos)
            mask |= ResourceObsidian;
        return mask;
    }

    int ResourceMaskForComponent(UPrimalHarvestingComponent* component)
    {
        if (!component) return ResourceNone;
        int mask = ResourceNone;
        auto addEntries = [&mask](TArray<FHarvestResourceEntry>& entries)
        {
            for (FHarvestResourceEntry& entry : entries)
                mask |= ResourceMaskForClass(entry.ResourceItem.uClass);
        };
        addEntries(component->HarvestResourceEntries());
        if (mask == ResourceNone) addEntries(component->BaseHarvestResourceEntries());
        return mask;
    }

    unsigned long long FoliageKey(const FOverlappedFoliageElement& element)
    {
        const unsigned long long componentKey = static_cast<unsigned long long>(
            reinterpret_cast<std::uintptr_t>(element.InstancedStaticMeshComponent));
        return (componentKey >> 4) ^
            (static_cast<unsigned long long>(static_cast<unsigned int>(element.HitBodyIndex)) * 0x9E3779B185EBCA87ULL);
    }

    void AddToSpot(const ResourceSource& source, const FOverlappedFoliageElement& element, int mask)
    {
        const double latitude = 50.0 + static_cast<double>(element.HarvestLocation.Y) / GpsScale;
        const double longitude = 50.0 + static_cast<double>(element.HarvestLocation.X) / GpsScale;
        const int latitudeCell = static_cast<int>(std::floor(latitude / SpotCellGps));
        const int longitudeCell = static_cast<int>(std::floor(longitude / SpotCellGps));
        const std::string key = std::string(source.id) + "_" + std::to_string(latitudeCell) + "_" + std::to_string(longitudeCell);

        ResourceSpot& spot = Spots[key];
        if (spot.id.empty())
        {
            spot.id = key;
            spot.sourceId = source.id;
            spot.realm = source.realm;
        }

        spot.expectedResources |= mask;
        spot.latitudeTotal += latitude;
        spot.longitudeTotal += longitude;
        ++spot.totalFoliage;
        const double health = element.MaxHarvestHealth > 0.01f
            ? std::max(0.0, std::min(1.0, static_cast<double>(element.CurrentHarvestHealth / element.MaxHarvestHealth)))
            : 1.0;
        if ((mask & ResourceMetal) != 0) { ++spot.metal; spot.metalHealth += health; }
        if ((mask & ResourceCrystal) != 0) { ++spot.crystal; spot.crystalHealth += health; }
        if ((mask & ResourceObsidian) != 0) { ++spot.obsidian; spot.obsidianHealth += health; }
    }

    void ScanSource(const ResourceSource& source)
    {
        UWorld* world = ArkApi::GetApiUtils().GetWorld();
        if (!world) return;

        const FVector origin(
            static_cast<float>((source.longitude - 50.0) * GpsScale),
            static_cast<float>((source.latitude - 50.0) * GpsScale),
            static_cast<float>(source.z));
        const float radius = static_cast<float>(source.searchRadiusGps * GpsScale);
        TArray<FOverlappedFoliageElement> foliage;
        FVector mutableOrigin = origin;
        UVictoryCore::ServerSearchFoliage(
            reinterpret_cast<UObject*>(world), &mutableOrigin, radius, &foliage,
            true, true, true, false, false);

        for (FOverlappedFoliageElement& element : foliage)
        {
            if (element.bIsUnharvestable || !element.bIsVisibleAndActive || element.CurrentHarvestHealth <= 0.01f)
                continue;
            const int mask = ResourceMaskForComponent(element.HarvestingComponent);
            if (mask == ResourceNone || !seenFoliage.insert(FoliageKey(element)).second) continue;
            AddToSpot(source, element, mask);
        }
    }

    void FinishManualScan()
    {
        const std::size_t rawSpotCount = Spots.size();
        for (auto it = Spots.begin(); it != Spots.end();)
        {
            if (it->second.totalFoliage < MinimumResourceRocks) it = Spots.erase(it);
            else ++it;
        }
        scanInProgress = false;
        nextSource = 0;
        ++completedGeneration;
        if (UWorld* world = ArkApi::GetApiUtils().GetWorld()) completedAt = world->TimeSecondsField();
        int metal = 0, crystal = 0, obsidian = 0;
        for (const auto& pair : Spots)
        {
            metal += pair.second.metal;
            crystal += pair.second.crystal;
            obsidian += pair.second.obsidian;
        }
        Log::GetLog()->info("Manual generation {} complete: spots={} ({} small spots removed), metal={}, crystal={}, obsidian={}, failures={}",
            completedGeneration, Spots.size(), rawSpotCount - Spots.size(), metal, crystal, obsidian, scanFailures);
    }

    void ScanNextSource()
    {
        if (!scanInProgress || !scanningEnabled || ArkApi::GetApiUtils().GetStatus() != ArkApi::ServerStatus::Ready || Sources.empty()) return;
        try
        {
            ScanSource(Sources[nextSource]);
        }
        catch (const std::exception& error)
        {
            ++scanFailures;
            lastError = error.what();
            Log::GetLog()->error("ResourceProbe manual scan failed: {}", error.what());
        }
        catch (...)
        {
            ++scanFailures;
            lastError = "unknown exception";
            Log::GetLog()->error("ResourceProbe manual scan failed with an unknown error");
        }

        ++nextSource;
        if (nextSource >= Sources.size()) FinishManualScan();
    }

    int HealthPercent(double totalHealth, int count)
    {
        return count > 0 ? static_cast<int>(std::round(totalHealth * 100.0 / count)) : 0;
    }

    FString BuildSnapshotReply()
    {
        std::ostringstream out;
        out << "OK=1\nPLUGIN_VERSION=1.4"
            << "\nMODE=MANUAL_ONLY"
            << "\nSCANNING_ENABLED=" << (scanningEnabled ? 1 : 0)
            << "\nIN_PROGRESS=" << (scanInProgress ? 1 : 0)
            << "\nREADY=" << (!scanInProgress && completedGeneration > 0 ? 1 : 0)
            << "\nGENERATION=" << completedGeneration
            << "\nNEXT_SOURCE=" << nextSource
            << "\nSOURCE_COUNT=" << Sources.size()
            << "\nSPOT_COUNT=" << Spots.size()
            << "\nFAILURES=" << scanFailures
            << "\nLAST_ERROR=" << lastError;
        if (completedGeneration > 0)
        {
            if (UWorld* world = ArkApi::GetApiUtils().GetWorld())
                out << "\nAGE_SECONDS=" << std::fixed << std::setprecision(1)
                    << static_cast<double>(std::max(static_cast<long double>(0), world->TimeSecondsField() - completedAt));
        }
        for (const auto& pair : Spots)
        {
            const ResourceSpot& spot = pair.second;
            const double latitude = spot.totalFoliage > 0 ? spot.latitudeTotal / spot.totalFoliage : 0;
            const double longitude = spot.totalFoliage > 0 ? spot.longitudeTotal / spot.totalFoliage : 0;
            out << "\nZONE=" << spot.id
                << "|SOURCE=" << spot.sourceId
                << "|REALM=" << spot.realm
                << "|LAT=" << std::fixed << std::setprecision(3) << latitude
                << "|LON=" << longitude
                << "|RADIUS=" << SpotRadiusGps
                << "|EXPECTED=" << spot.expectedResources
                << "|SCANNED=1"
                << "|METAL=" << spot.metal
                << "|METAL_HP=" << HealthPercent(spot.metalHealth, spot.metal)
                << "|CRYSTAL=" << spot.crystal
                << "|CRYSTAL_HP=" << HealthPercent(spot.crystalHealth, spot.crystal)
                << "|OBSIDIAN=" << spot.obsidian
                << "|OBSIDIAN_HP=" << HealthPercent(spot.obsidianHealth, spot.obsidian)
                << "|FOLIAGE=" << spot.totalFoliage;
        }
        return FString(out.str());
    }

    void SnapshotRcon(RCONClientConnection* connection, RCONPacket* packet, UWorld*)
    {
        SendRconReply(connection, packet->Id, BuildSnapshotReply());
    }

    void RefreshRcon(RCONClientConnection* connection, RCONPacket* packet, UWorld*)
    {
        if (!scanningEnabled)
        {
            SendRconReply(connection, packet->Id, FString("OK=0\nMESSAGE=scanning is paused"));
            return;
        }
        Spots.clear();
        seenFoliage.clear();
        nextSource = 0;
        completedAt = 0;
        lastError.clear();
        scanInProgress = true;
        Log::GetLog()->info("Manual resource scan requested by RCON");
        SendRconReply(connection, packet->Id, FString("OK=1\nMODE=MANUAL_ONLY\nIN_PROGRESS=1\nMESSAGE=manual scan scheduled"));
    }

    void PauseRcon(RCONClientConnection* connection, RCONPacket* packet, UWorld*)
    {
        scanningEnabled = false;
        Log::GetLog()->warn("Resource scanning paused by RCON");
        SendRconReply(connection, packet->Id, FString("OK=1\nSCANNING_ENABLED=0\nMESSAGE=scanning paused"));
    }

    void ResumeRcon(RCONClientConnection* connection, RCONPacket* packet, UWorld*)
    {
        scanningEnabled = true;
        Log::GetLog()->warn("Resource scanning enabled by RCON; no scan starts until Refresh is requested");
        SendRconReply(connection, packet->Id, FString("OK=1\nSCANNING_ENABLED=1\nMODE=MANUAL_ONLY\nMESSAGE=scanning enabled"));
    }

    void DiagnosticsRcon(RCONClientConnection* connection, RCONPacket* packet, UWorld*)
    {
        SendRconReply(connection, packet->Id, BuildSnapshotReply());
    }

    void Load()
    {
        Log::Get().Init("ResourceProbe");
        ArkApi::GetCommands().AddRconCommand("ResourceProbe.Scan", &SnapshotRcon);
        ArkApi::GetCommands().AddRconCommand("ResourceProbe.Refresh", &RefreshRcon);
        ArkApi::GetCommands().AddRconCommand("ResourceProbe.Pause", &PauseRcon);
        ArkApi::GetCommands().AddRconCommand("ResourceProbe.Resume", &ResumeRcon);
        ArkApi::GetCommands().AddRconCommand("ResourceProbe.Diagnostics", &DiagnosticsRcon);
        ArkApi::GetCommands().AddOnTimerCallback("ResourceProbe.Tick", &ScanNextSource);
        Log::GetLog()->info("ResourceProbe 1.4 loaded in manual-only mode; spots with 5 or fewer resource rocks are hidden");
    }

    void Unload()
    {
        ArkApi::GetCommands().RemoveOnTimerCallback("ResourceProbe.Tick");
        ArkApi::GetCommands().RemoveRconCommand("ResourceProbe.Scan");
        ArkApi::GetCommands().RemoveRconCommand("ResourceProbe.Refresh");
        ArkApi::GetCommands().RemoveRconCommand("ResourceProbe.Pause");
        ArkApi::GetCommands().RemoveRconCommand("ResourceProbe.Resume");
        ArkApi::GetCommands().RemoveRconCommand("ResourceProbe.Diagnostics");
    }
}

BOOL APIENTRY DllMain(HMODULE, DWORD reason, LPVOID)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH: Load(); break;
    case DLL_PROCESS_DETACH: Unload(); break;
    }
    return TRUE;
}
