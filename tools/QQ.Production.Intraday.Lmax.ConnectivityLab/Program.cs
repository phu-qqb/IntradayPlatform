using QQ.Production.Intraday.Lmax.ConnectivityLab;

var runner = new LmaxConnectivityLabRunner(
    new PlaceholderLmaxPublicDataClient(),
    new PlaceholderLmaxAccountClient(),
    new PlaceholderLmaxFixSessionClient(),
    new LmaxConnectivityLabSafetyValidator());

return await runner.RunAsync(args, CancellationToken.None);
