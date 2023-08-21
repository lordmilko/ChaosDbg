namespace ChaosDbg.MSBuild
{
    class ReactiveCommandInfo
    {
        /// <summary>
        /// Gets the namespace the class is contained in.
        /// </summary>
        public string Namespace { get; }

        /// <summary>
        /// Gets the name of the class containing the method to create a reactive command from.
        /// </summary>
        public string ClassName { get; }

        /// <summary>
        /// Gets the name of the method that should be the target of the reactive command.
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Gets the name to use for the reactive command property.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets the field to use for the reactive command field.
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// Gets the name of the function that should be passed as the canExecute parameter of the relay command.
        /// If null, no value is passed
        /// </summary>
        public string CanExecuteName { get; }

        /// <summary>
        /// Gets the type to use for the field/property.
        /// </summary>
        public string PropertyType { get; } = "IRelayCommand";

        /// <summary>
        /// Gets the implementing type to use for the field/property.
        /// </summary>
        public string CommandType { get; } = "RelayCommand";

        public ReactiveCommandInfo(string ns, string className, string methodName, string propertyName, string fieldName, string canExecuteName)
        {
            Namespace = ns;
            ClassName = className;
            MethodName = methodName;
            PropertyName = propertyName;
            FieldName = fieldName;
            CanExecuteName = canExecuteName;
        }
    }
}
