using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace chaos
{
    class CommandBuilder
    {
        private IServiceProvider serviceProvider;

        public CommandBuilder(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }


        public Parser Build()
        {
            var types = typeof(Program).Assembly.GetTypes();

            var rootCommand = new RootCommand();

            foreach (var type in types)
            {
                var methods = type.GetMethods();

                var ctor = type.GetConstructors().Single();

                var instance = new Lazy<object>(() =>
                {
                    var ctorParameters = ctor.GetParameters().Select(p => serviceProvider.GetService(p.ParameterType)).ToArray();
                    return Activator.CreateInstance(type, ctorParameters);
                });

                foreach (var method in methods)
                {
                    var commandAttrib = method.GetCustomAttribute<CommandAttribute>();

                    if (commandAttrib != null)
                    {
                        var cliCommand = new Command(commandAttrib.Name);

                        var parameters = method.GetParameters();

                        foreach (var parameter in parameters)
                        {
                            var parameterAttrib = parameter.GetCustomAttribute<ParameterAttribute>();

                            if (parameterAttrib == null)
                                throw new NotImplementedException($"Parameter {type.Name}.{method.Name} -> {parameter.Name} was missing a {nameof(ParameterAttribute)}");

                            var cliOptType = typeof(Option<>).MakeGenericType(parameter.ParameterType);

                            var cliOpt = (Option) Activator.CreateInstance(cliOptType, new object[] {parameterAttrib.Name, null});

                            cliCommand.AddOption(cliOpt);
                        }

                        cliCommand.SetHandler(ctx =>
                        {
                            var vals = cliCommand.Options.Select(v => ctx.ParseResult.GetValueForOption(v)).ToArray();

                            try
                            {
                                method.Invoke(instance.Value, vals);
                            }
                            catch (TargetInvocationException ex)
                            {
                                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                            }
                        });

                        rootCommand.AddCommand(cliCommand);
                    }
                }
            }

            var commandLineBuilder = new CommandLineBuilder(rootCommand);

            commandLineBuilder
                .AddMiddleware(async (context, next) =>
                {
                    try
                    {
                        await next(context);
                    }
                    catch (Exception ex)
                    {
                        Console.ResetColor();
                        Console.ForegroundColor = ConsoleColor.Red;

                        context.Console.Error.WriteLine(ex.ToString());

                        Console.ResetColor();
                    }
                });

            var parser = commandLineBuilder.Build();

            return parser;
        }
    }
}
