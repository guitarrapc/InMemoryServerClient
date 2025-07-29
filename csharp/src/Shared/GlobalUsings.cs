global using MessagePack;
global using System.Runtime.CompilerServices;

// Make internal members visible to test assemblies
[assembly: InternalsVisibleTo("BattleLogic.Tests")]
[assembly: InternalsVisibleTo("CliClient.Tests")]
[assembly: InternalsVisibleTo("InMemoryServer.Tests")]
[assembly: InternalsVisibleTo("E2E.Tests")]
