NCli
====

A very simple command line parsing library that is verb oriented, and dependency injection friendly.

Grammar:

``` bash
> command do this
> command do --longOptionName someValue # long option value
> command do -l someValue # same option as above
> command do -r 8 # ints
> command do --IEnumerableOption value1 value2 # list values can't start with -
> command do -q # this is a boolean flag.
```

Code for above:

``` csharp
class DoVerb : IVerb
{
    public string OriginalVerb { get; set; }

    [Option(0)] // This is an ordered parameter.
    public string ThingToDo { get; set; }

    [Option('l', "longOptionName", HelpText = "This is the help for this option")]
    public string SomeOption { get; set;}

    [Option('r')]
    public int RetryCount { get; set; }

    [Option('q')]
    public bool Quiet { get; set; }

    [Option("IEnumerableOption")]
    public IEnumerable<string> ListOfOptions { get; set; }

    private readonly IManager _manager; // will be injected

    public DoVerb(IManager manager) // for dependency injection
    {
        _manager = manager;
    }

    public async Task Run()
    {
        // do stuff

        Assert.NotNull(_manager);

        Assert.Equal(OriginalVerb, "do");
        Assert.Equal(ThingToDo, "this");
        Assert.Equal(SomeOption, "someValue");
        Assert.Equal(RetryCount, 8);
        Assert.Equal(Quite, true);
        // ListOfOptions == new List<string> { "value1", "value2" }
    }
}
```


Wiring it up:

``` csharp
class MyDependencyResolver : IDependencyResolver
{
    private readonly IContainer _container;

    public DependencyResolver(IContainer container)
    {
        _container = container;
    }

    public object GetService(Type type)
    {
        return _container.Resolve(type);
    }

    public T GetService<T>()
    {
        return _container.Resolve<T>();
    }
}

class Program
{
    public static Main(string[] args)
    {
        IContainer _container = // create your container
        ArgsParser.DependencyResolver = new MyDependencyResolver(_container); // You have to implement IDependencyResolver
        var verb = ArgsParser.Parse(args);
        Task.Run(() => verb.Run()).Wait();
    }
}
```

The example above is using Autofac, but any IoC container should work if you implement `IDependencyResolver`.
