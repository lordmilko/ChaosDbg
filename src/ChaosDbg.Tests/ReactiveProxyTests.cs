using System;
using ChaosDbg.Reactive;
using ChaosDbg.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class ReactiveProxyTests
    {
        [TestMethod]
        public void ReactiveProxy_Success()
        {
            var proxy = ViewModelProvider.Create<SuccessViewModel>();
            bool eventCalled = false;

            proxy.PropertyChanged += (s, e) => eventCalled = true;
            Assert.IsNull(proxy.Value);
            proxy.Value = "foo";

            Assert.AreEqual("foo", proxy.Value);
            Assert.IsTrue(eventCalled);
        }

        [TestMethod]
        public void ReactiveProxy_SetValueInCtor()
        {
            var proxy = ViewModelProvider.Create<SetValueInCtorViewModel>();

            Assert.AreEqual(proxy.Value, "val");
        }

        [TestMethod]
        public void ReactiveProxy_NoReactiveProperties()
        {
            var proxy = ViewModelProvider.Create<NoReactivePropertiesViewModel>();
        }

        [TestMethod]
        public void ReactiveProxy_ReactiveProperty_NotVirtual_Throws()
        {
            AssertEx.Throws<InvalidOperationException>(
                () => ViewModelProvider.Create<NonVirtualPropertyViewModel>(),
                "Property 'NonVirtualPropertyViewModel.Value' was marked with a ReactiveAttribute however it is not marked as virtual."
            );
        }

        [TestMethod]
        public void ReactiveProxy_ReactiveProperty_MissingSetter_Throws()
        {
            AssertEx.Throws<InvalidOperationException>(
                () => ViewModelProvider.Create<MissingSetterViewModel>(),
                "Property 'MissingSetterViewModel.Value' was marked with a ReactiveAttribute however does not have a setter."
            );
        }

        [TestMethod]
        public void ReactiveProxy_ReactiveProperty_StartsWithLowercase_Throws()
        {
            AssertEx.Throws<InvalidOperationException>(
                () => ViewModelProvider.Create<StartsWithLowercaseViewModel>(),
                "Property 'StartsWithLowercaseViewModel.value' was marked with a ReactiveAttribute however does not have a setter."
            );
        }

        [TestMethod]
        public void ReactiveProxy_TypeNotViewModel_Throws()
        {
            AssertEx.Throws<InvalidOperationException>(
                () => ReactiveProxyBuilder.Build(typeof(string)),
                "Cannot create reactive proxy for type 'String': type does not derive from 'ReactiveObject' or 'ViewModelBase'."
            );
        }
    }
}
