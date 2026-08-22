# Starting the server for the API suite

The server must be started in API-test configuration:

    ASPNETCORE_ENVIRONMENT=Testing dotnet run --project src/ChurchProjection.Api \
      --no-launch-profile \
      --Access:TestPin=123456 \
      --Access:RequirePairingFromLoopback=true

The environment has to come from `ASPNETCORE_ENVIRONMENT`, and the launch
profile has to be off. `launchSettings.json` pins the environment to
Development, which silently skips `DevSeed` and leaves the suite asserting
against an empty library; a `--environment Testing` argument does not survive
either, so the run starts as Production and the test-only settings below are
refused at startup. `--no-launch-profile` also drops the profile's
`applicationUrl`, so set `ASPNETCORE_URLS=http://localhost:5000` if 5000 is
not already the default on this machine.

Restart the server between full runs: the pairing limiter holds its counts in
memory, and one complete pass spends enough of the hourly global budget
(NFR-SEC-05) that the next pass answers 429. Delete
`src/ChurchProjection.Api/data/projection.db*` first as well — the suite
asserts against seeded state, not accumulated state.

`Access:TestPin` fixes the PIN so the suite does not have to read it from
`GET /api/pin`, which is loopback-only and itself behind the pair gate.

`Access:RequirePairingFromLoopback` switches off the loopback exemption
(FR-SEC-08). Without it the whole suite runs exempt from pairing and
SYS-SEC-03 cannot fail.

Both settings are test-only and are refused when the environment is
Production. A test convenience that survives into a real start is not a test
convenience.

Port 5000 is the ASP.NET Core default for HTTP. If launchSettings.json is
changed, change baseUrl with it.
