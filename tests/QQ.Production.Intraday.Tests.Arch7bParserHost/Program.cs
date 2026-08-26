using QQ.Production.Intraday.Infrastructure.PostgreSql;

try
{
    Environment.ExitCode = await Arch7bPrearmedFreshSlotHandoffCli.RunAsync(args);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}
