dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults/

dotnet reportgenerator -reports:./TestResults/**/coverage.cobertura.xml -targetdir:./CoverageReport -reporttypes:Html
