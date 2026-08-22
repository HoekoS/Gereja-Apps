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

`keys\` is derived from the database folder and needs no setting. The other two
are set in `C:\ChurchProjection\appsettings.json`, and both are written out in
full so that the folder above is provably the folder the service writes to.
`media` must sit beside `projection.db`, or the backup copies the database
without the clips it points at:

```json
{
  "Storage": {
    "DatabasePath": "C:\\ChurchProjection\\data\\projection.db",
    "MediaRoot": "C:\\ChurchProjection\\data\\media"
  }
}
```

A relative path here is resolved against the install folder, not against the
service's working directory (which Windows sets to `C:\Windows\System32`), so
leaving the shipped defaults alone puts the same two folders in the same place.
Writing them out is belt and braces for anyone who installs somewhere else.

## The PIN

Shown at `http://localhost:5000/api/pin`, readable only from the booth
machine. It rotates on the first request after Saturday midnight. Rotation
signs everyone out — that is the point.

## When something is wrong on Sunday

1. `http://localhost:5000/healthz` — if it answers, the server is fine and
   the problem is on the client's network.
2. Restart the service. Live state is stored in the database, so nothing is
   lost by restarting.
