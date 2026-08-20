# Starting the server for the API suite

The server must be started in API-test configuration:

    dotnet run --project src/ChurchProjection.Api \
      --environment Testing \
      --Access:TestPin=123456 \
      --Access:RequirePairingFromLoopback=true

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
