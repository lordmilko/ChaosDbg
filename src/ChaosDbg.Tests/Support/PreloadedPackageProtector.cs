using System;
using System.Reflection;
using System.Windows;

namespace ChaosDbg.Tests
{
    class PreloadedPackageProtector : IDisposable
    {
        //During Application.Shutdown() it will call PreloadedPackages.Clear(). This causes the preloaded packages map to null.
        //PreloadedPackages is normally populated during static ctors, so it will never get re-populated again. As such, we must
        //store its contents before it is removed
        private static FieldInfo packagePairsField;

        static PreloadedPackageProtector()
        {
            var preloadedPackagesType = typeof(PresentationSource).Assembly.GetType("MS.Internal.IO.Packaging.PreloadedPackages");

            if (preloadedPackagesType == null)
                throw new InvalidOperationException("PreloadedPackages type could not be found");

            packagePairsField = preloadedPackagesType.GetField("_packagePairs", BindingFlags.Static | BindingFlags.NonPublic);

            if (packagePairsField == null)
                throw new MissingMemberException(preloadedPackagesType.Name, "_packagePairs");
        }

        private object value;

        public PreloadedPackageProtector()
        {
            value = packagePairsField.GetValue(null);
        }

        public void Dispose()
        {
            packagePairsField.SetValue(null, value);
        }
    }
}
