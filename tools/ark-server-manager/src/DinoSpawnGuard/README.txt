DinoSpawnGuard (ARK: Survival Evolved / ArkApi)

The plugin observes AShooterGameMode.LastTimeSavedWorld once per second. It does
not scan at startup. When a completed save is observed, it waits three seconds,
collects active major wild dinosaurs from the configured whitelist, and checks
50 dinosaurs per timer tick for blocking geometry.

Only living, untamed, unattached, whitelisted dinosaurs are eligible. Flyers,
fully aquatic creatures, bosses, alpha Rex/Carno, Rock Elementals and other
special actors are not included by default. If a collision is found, nearby
positions in the same world-space realm are tested with FindTeleportSpot and
EncroachingBlockingGeometry. A dino is teleported only after a candidate is
verified. No dino is destroyed when relocation fails.

RCON commands:
  DinoSpawnGuard.Status  Shows mode, counters and last result.
  DinoSpawnGuard.Pause   Immediately cancels and disables scanning/relocation.
  DinoSpawnGuard.Resume  Re-enables it; waits for the next completed save.

There is intentionally no manual scan command: relocation is triggered only
after the server reports a newly completed world save.

Emergency rollback: stop the ARK server, then rename or remove the
DinoSpawnGuard plugin directory. ResourceProbe is independent.
