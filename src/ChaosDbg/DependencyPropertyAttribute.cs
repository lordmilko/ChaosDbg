using System;
using System.Windows;

namespace ChaosDbg
{
    /// <summary>
    /// Specifies an attached dependency property that should be automatically generated for a given class.<para/>
    /// Properties will be generated using DependencyProperty.RegisterAttached() unless <see cref="DependencyPropertyAttribute.IsReadOnly"/> is specified,
    /// in which case DependencyProperty.RegisterAttachedReadOnly() will be used instead.
    /// </summary>
    public class AttachedDependencyPropertyAttribute : DependencyPropertyAttribute
    {
        public AttachedDependencyPropertyAttribute(string name, Type type) : base(name, type)
        {
        }
    }

    /// <summary>
    /// Specifies a dependency property that should be automatically generated for a given class.<para/>
    /// Properties will be generated using DependencyProperty.Register() unless <see cref="IsReadOnly"/> is specified,
    /// in which case DependencyProperty.RegisterReadOnly() will be used instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class DependencyPropertyAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the dependency property.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type of value that can be stored in the property.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets or sets a default value literal that should be assigned to the property.<para/>
        /// This property cannot be used in conjunction with <see cref="DefaultValueExpression"/>.
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets a complex expression that should be used as the default value of the property.<para/>
        /// This property cannot be used in conjunction with <see cref="DefaultValue"/>.
        /// </summary>
        public string DefaultValueExpression { get; set; }

        /// <summary>
        /// Gets or sets whether this is a read-only property that should be created using DependencyProperty.RegisterReadOnly().
        /// </summary>
        public bool IsReadOnly { get; set; }

        #region FrameworkPropertyMetadataOptions

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.AffectsMeasure"/> should be specified on the dependency property.
        /// </summary>
        public bool AffectsMeasure { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.AffectsArrange"/> should be specified on the dependency property.
        /// </summary>
        public bool AffectsArrange { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.AffectsParentMeasure"/> should be specified on the dependency property.
        /// </summary>
        public bool AffectsParentMeasure { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.AffectsParentArrange"/> should be specified on the dependency property.
        /// </summary>
        public bool AffectsParentArrange { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.AffectsRender"/> should be specified on the dependency property.
        /// </summary>
        public bool AffectsRender { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.Inherits"/> should be specified on the dependency property.
        /// </summary>
        public bool Inherits { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.OverridesInheritanceBehavior"/> should be specified on the dependency property.
        /// </summary>
        public bool OverridesInheritanceBehavior { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.NotDataBindable"/> should be specified on the dependency property.
        /// </summary>
        public bool NotDataBindable { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.BindsTwoWayByDefault"/> should be specified on the dependency property.
        /// </summary>
        public bool BindsTwoWayByDefault { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.Journal"/> should be specified on the dependency property.
        /// </summary>
        public bool Journal { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="FrameworkPropertyMetadataOptions.SubPropertiesDoNotAffectRender"/> should be specified on the dependency property.
        /// </summary>
        public bool SubPropertiesDoNotAffectRender { get; set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyPropertyAttribute"/> class.
        /// </summary>
        /// <param name="name">The name to use for the property. This should not contain a "Property" suffix.</param>
        /// <param name="type">The type of value that can be stored in the property.</param>
        public DependencyPropertyAttribute(string name, Type type)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Name = name;
            Type = type;
        }
    }
}
