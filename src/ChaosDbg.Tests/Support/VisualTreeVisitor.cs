using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ChaosDbg.Tests
{
    abstract class VisualTreeVisitor<T>
    {
        private Dictionary<Type, Func<DependencyObject, T>> dispatchers = new Dictionary<Type, Func<DependencyObject, T>>();

        private HashSet<object> seen = new HashSet<object>();

        protected VisualTreeVisitor()
        {
            Add<TestWindow>(VisitWindow);

            dispatchers.Add(typeof(Border), VisitIgnored);
            dispatchers.Add(typeof(AdornerDecorator), VisitIgnored);
            dispatchers.Add(typeof(AdornerLayer), VisitIgnored);
            dispatchers.Add(typeof(ContentPresenter), VisitIgnored);
            dispatchers.Add(typeof(DockPanel), VisitIgnored);
        }

        public T Visit(DependencyObject dependencyObject)
        {
            if (dependencyObject == null)
                return default;

            if (!seen.Add(dependencyObject))
                throw new NotImplementedException($"Object {dependencyObject} has already been seen in the tree. This indicates some kind of bug in our tree parsing logic");

            if (dispatchers.TryGetValue(dependencyObject.GetType(), out var dispatcher))
                return dispatcher(dependencyObject);

            return VisitUnknown(dependencyObject);
        }

        public void Add<TControl>(Func<TControl, T> func) where TControl : DependencyObject
        {
            dispatchers.Add(typeof(TControl), d => func((TControl) d));
        }

        public virtual T VisitWindow(TestWindow window) => VisitIgnored(window);

        public T VisitUnknown(DependencyObject dependencyObject)
        {
            throw new NotImplementedException($"Don't know how to handle dependency object of type '{dependencyObject.GetType().Name}'.");
        }

        protected T VisitIgnored(DependencyObject dependencyObject)
        {
            var results = VisitChildren(dependencyObject);

            if (results.Length == 0)
                return default;

            if (results.Length == 1)
                return results[0];

            return CreateResultCollection(results);
        }

        protected abstract T CreateResultCollection(T[] results);

        public T[] VisitChildren(DependencyObject dependencyObject)
        {
            var children = dependencyObject.GetVisualChildren().ToArray();

            var results = new List<T>();

            foreach (var child in children)
            {
                var result = Visit(child);

                if (result != null)
                {
                    if (result is IVisitorResultCollection<T> c)
                        results.AddRange(c.Children);
                    else
                        results.Add(result);
                }
            }

            return results.ToArray();
        }
    }
}
