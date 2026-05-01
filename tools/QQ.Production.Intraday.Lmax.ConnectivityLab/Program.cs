using QQ.Production.Intraday.Lmax.ConnectivityLab;

var runner = new LmaxConnectivityLabRunner(
    new PlaceholderLmaxPublicDataClient(),
    new PlaceholderLmaxAccountClient(),
    new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator()),
    new LmaxConnectivityLabSafetyValidator());

return await runner.RunAsync(args, CancellationToken.None);
