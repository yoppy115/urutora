ResourceProbe (ARK: Survival Evolved / ArkApi)

RCON commands:
  ResourceProbe.Scan     Returns the current manual-scan state or completed snapshot.
  ResourceProbe.Refresh  Clears the cache and starts one manual incremental scan.
  ResourceProbe.Pause    Stops all resource scanning until resumed or restarted.
  ResourceProbe.Resume   Re-enables scanning; it does not start a scan by itself.
  ResourceProbe.Diagnostics  Returns version, state, failures, last error and snapshots.

No scan runs at startup or on a schedule. A scan runs only after ResourceProbe.Refresh.
The broad rich regions are checked incrementally, then actual node positions are divided
into 0.35-GPS cells (about 0.50 GPS corner-to-corner) for the returned small spots.
Cells containing five or fewer unique resource rocks are omitted. Each returned pin uses
the average of the rocks' actual world positions rather than the geometric cell centre.
It is read-only: it does not harvest, damage, spawn, destroy, or modify any world object.
