# Booth Runbook

## How the booth runs it

`publish/booth/ChurchProjection.Api.exe`, installed as a Windows Service so it
starts before anyone logs in:

```
sc.exe create ChurchProjection binPath= "C:\ChurchProjection\ChurchProjection.Api.exe" start= auto
sc.exe description ChurchProjection "Church service projection server"
sc.exe start ChurchProjection
```

No Docker. No Redis. No .NET runtime install. If the machine boots, the
server is up.

## Where the data lives

`C:\ChurchProjection\data\` — `projection.db`, `media\`, `keys\`. Backing up
means copying that folder while the service is stopped. A backup is proven
by restoring it onto another machine and starting the server there (INT-12),
never by the copy succeeding.

## The PIN

Shown at `http://localhost:5000/api/pin`, readable only from the booth
machine. It rotates on the first request after Saturday midnight. Rotation
signs everyone out — that is the point.

## When something is wrong on Sunday

1. `http://localhost:5000/healthz` — if it answers, the server is fine and
   the problem is on the client's network.
2. Restart the service. Live state is stored in the database, so nothing is
   lost by restarting.
