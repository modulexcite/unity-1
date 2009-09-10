﻿//===============================================================================
// Microsoft patterns & practices
// Unity Application Block
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================

using System.ComponentModel;
using Microsoft.Practices.Unity.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Practices.Unity.InterceptionExtension.Tests.VirtualMethodInterception
{
    /// <summary>
    /// Tests for the virtual method interception mechanism as invoked
    /// through the container.
    /// </summary>
    [TestClass]
    public class ContainerVirtualMethodInterceptionFixture
    {
        [TestMethod]
        public void InterceptedClassGetsReturned()
        {
            CallCountHandler h1 = new CallCountHandler();
            CallCountHandler h2 = new CallCountHandler();

            IUnityContainer container = GetConfiguredContainer(h1, h2);
            AddPoliciesToContainer(container);
            ConfigureInterceptionWithRegisterType(container);

            Interceptee foo = container.Resolve<Interceptee>();

            Assert.AreNotSame(typeof(Interceptee), foo.GetType());
        }

        [TestMethod]
        public void AttachedHandlersAreCalled()
        {
            CallCountHandler h1 = new CallCountHandler();
            CallCountHandler h2 = new CallCountHandler();

            IUnityContainer container = GetConfiguredContainer(h1, h2);
            AddPoliciesToContainer(container);
            ConfigureInterceptionWithRegisterType(container);

            Interceptee foo = container.Resolve<Interceptee>();

            int oneCount = 0;
            int twoCount = 0;

            for (oneCount = 0; oneCount < 2; ++oneCount)
            {
                foo.MethodOne();
            }

            for (twoCount = 0; twoCount < 3; ++twoCount)
            {
                foo.MethodTwo("hi", twoCount);
            }

            Assert.AreEqual(oneCount, h1.CallCount);
            Assert.AreEqual(twoCount, h2.CallCount);
        }

        [TestMethod]
        public void RegisteringInterceptionOnOpenGenericsLetsYouResolveMultipleClosedClasses()
        {
            IUnityContainer container = new UnityContainer()
                .AddNewExtension<Interception>();

            AddPoliciesToContainer(container);

            container.Configure<Interception>()
                .SetDefaultInterceptorFor(typeof(GenericFactory<>), new VirtualMethodInterceptor());

            GenericFactory<SubjectOne> resultOne = container.Resolve<GenericFactory<SubjectOne>>();
            GenericFactory<SubjectTwo> resultTwo = container.Resolve<GenericFactory<SubjectTwo>>();

            Assert.IsTrue(resultOne is IInterceptingProxy);
            Assert.IsTrue(resultTwo is IInterceptingProxy);

            Assert.AreEqual("**Hi**", resultTwo.MakeAT().GetAValue("Hi"));
        }

        [TestMethod]
        public virtual void TestNewVirtualOverride()
        {
            IUnityContainer container = GetContainer();

            NewVirtualOverrideTestClass testClass = container.Resolve<NewVirtualOverrideTestClass>();

            Assert.IsTrue(testClass.TestMethod1(), "override");
            Assert.IsTrue(testClass.TestMethod2(), "new virtual");
            Assert.IsTrue(testClass.TestMethod3(), "always true");
            Assert.IsTrue(testClass.TestMethod4(), "abstract");

            Assert.AreEqual(4, container.Resolve<CallCountHandler>("TestCallHandler").CallCount);
        }

        [TestMethod]
        public void CanInterceptWithInterceptorSetAsDefaultForBaseClassWithMultipleImplementations()
        {
            IUnityContainer container =
                new UnityContainer()
                    .RegisterType<BaseClass, ImplementationOne>("one")
                    .RegisterType<BaseClass, ImplementationTwo>("two")
                    .AddNewExtension<Interception>()
                    .Configure<Interception>()
                        .SetDefaultInterceptorFor<BaseClass>(new VirtualMethodInterceptor())
                    .Container;

            BaseClass instanceOne = container.Resolve<BaseClass>("one");
            BaseClass instanceTwo = container.Resolve<BaseClass>("two");

            Assert.AreEqual("ImplementationOne", instanceOne.Method());
            Assert.AreEqual("ImplementationTwo", instanceTwo.Method());
        }

        [TestMethod]
        public void CanAddInterceptionBehaviorsWithRequiredInterfaces()
        {
            IUnityContainer container =
                new UnityContainer()
                    .AddNewExtension<Interception>()
                    .RegisterType<ClassWithVirtualProperty>(
                        new Interceptor(new VirtualMethodInterceptor()),
                        new InterceptionBehavior(
                            new DelegateInterceptionBehaviorDescriptor(
                                (i, t1, t2, c) => new NaiveINotifyPropertyChangedInterceptionBehavior())));

            ClassWithVirtualProperty instance = container.Resolve<ClassWithVirtualProperty>();

            string changedProperty = null;
            ((INotifyPropertyChanged)instance).PropertyChanged += (s, a) => changedProperty = a.PropertyName;

            instance.Property = 10;

            Assert.AreEqual("Property", changedProperty);
        }

        [TestMethod]
        public void ResolvingKeyForTheSecondTimeAfterAddingBehaviorWithRequiredInterfaceReflectsLastConfiguration()
        {
            IUnityContainer container =
                new UnityContainer()
                    .AddNewExtension<Interception>()
                    .RegisterType<ClassWithVirtualProperty>(new Interceptor(new VirtualMethodInterceptor()));

            Assert.IsFalse(container.Resolve<ClassWithVirtualProperty>() is INotifyPropertyChanged);

            container
                .RegisterType<ClassWithVirtualProperty>(
                    new InterceptionBehavior(
                        new DelegateInterceptionBehaviorDescriptor(
                            (i, t1, t2, c) => new NaiveINotifyPropertyChangedInterceptionBehavior())));

            Assert.IsTrue(container.Resolve<ClassWithVirtualProperty>() is INotifyPropertyChanged);
        }

        [TestMethod]
        public void GeneratedDerivedTypeIsCached()
        {
            IUnityContainer container =
                new UnityContainer()
                    .AddNewExtension<Interception>()
                    .RegisterType<ClassWithVirtualProperty>(new Interceptor(new VirtualMethodInterceptor()));

            ClassWithVirtualProperty instanceOne = container.Resolve<ClassWithVirtualProperty>();
            ClassWithVirtualProperty instanceTwo = container.Resolve<ClassWithVirtualProperty>();

            Assert.AreSame(typeof(ClassWithVirtualProperty), instanceOne.GetType().BaseType);
            Assert.AreSame(instanceOne.GetType(), instanceTwo.GetType());
        }

        [TestMethod]
        public void DescriptorsAreQueriedForInterceptorsOnEachBuildUp()
        {
            int timesDescriptorInvoked = 0;

            IUnityContainer container =
                new UnityContainer()
                    .AddNewExtension<Interception>()
                    .RegisterType<ClassWithVirtualProperty>(
                        new Interceptor(new VirtualMethodInterceptor()),
                        new InterceptionBehavior(
                            new DelegateInterceptionBehaviorDescriptor((i, t1, t2, c) =>
                            {
                                timesDescriptorInvoked++;
                                return new DelegateInterceptionBehavior((mi, gn) => gn()(mi, gn));
                            })));

            ClassWithVirtualProperty instanceOne = container.Resolve<ClassWithVirtualProperty>();
            ClassWithVirtualProperty instanceTwo = container.Resolve<ClassWithVirtualProperty>();

            Assert.AreEqual(2, timesDescriptorInvoked);
        }

        [TestMethod]
        public void CanInterceptClassWithSingleNonDefaultConstructor()
        {
            CallCountInterceptionBehavior callCountBehavior = new CallCountInterceptionBehavior();

            IUnityContainer container =
                new UnityContainer()
                    .AddNewExtension<Interception>()
                    .RegisterType<ClassWithSingleNonDefaultConstructor>(
                        new InjectionConstructor("some value"),
                        new Interceptor(new VirtualMethodInterceptor()),
                        new InterceptionBehavior(callCountBehavior));

            ClassWithSingleNonDefaultConstructor instance = container.Resolve<ClassWithSingleNonDefaultConstructor>();

            string value = instance.GetValue();

            Assert.AreEqual("some value", value);
            Assert.AreEqual(1, callCountBehavior.CallCount);
        }

        [TestMethod]
        [Ignore]    // determine whether this should be enabled.
        public void CanInterceptClassWithInternalConstructor()
        {
            IUnityContainer container = new UnityContainer();

            ClassWithInternalConstructor nonInterceptedInstance = container.Resolve<ClassWithInternalConstructor>();

            Assert.AreEqual(10, nonInterceptedInstance.Method());

            CallCountInterceptionBehavior callCountBehavior = new CallCountInterceptionBehavior();
            container
                .AddNewExtension<Interception>()
                .RegisterType<ClassWithInternalConstructor>(
                    new Interceptor(new VirtualMethodInterceptor()),
                    new InterceptionBehavior(callCountBehavior));

            ClassWithInternalConstructor interceptedInstance = container.Resolve<ClassWithInternalConstructor>();

            Assert.AreEqual(10, interceptedInstance.Method());
            Assert.AreEqual(1, callCountBehavior.CallCount);
        }

        protected virtual IUnityContainer GetContainer()
        {
            IUnityContainer container = new UnityContainer()
                .AddNewExtension<Interception>()
                .RegisterType<IMatchingRule, AlwaysMatchingRule>("AlwaysMatchingRule")
                .RegisterType<ICallHandler, CallCountHandler>("TestCallHandler", new ContainerControlledLifetimeManager());

            container.Configure<Interception>()
                .SetDefaultInterceptorFor<NewVirtualOverrideTestClass>(new VirtualMethodInterceptor())
                .AddPolicy("Rules")
                .AddMatchingRule("AlwaysMatchingRule")
                .AddCallHandler("TestCallHandler");

            return container;
        }

        private IUnityContainer GetConfiguredContainer(ICallHandler h1, ICallHandler h2)
        {
            IUnityContainer container = new UnityContainer()
                .AddNewExtension<Interception>()
                .RegisterInstance<ICallHandler>("h1", h1)
                .RegisterInstance<ICallHandler>("h2", h2);

            return container;
        }

        private IUnityContainer AddPoliciesToContainer(IUnityContainer container)
        {
            container.Configure<Interception>()
                .AddPolicy("MethodOne")
                .AddMatchingRule<MemberNameMatchingRule>(new InjectionConstructor("MethodOne"))
                .AddCallHandler("h1")
                .Interception
                .AddPolicy("MethodTwo")
                .AddMatchingRule<MemberNameMatchingRule>(new InjectionConstructor("MethodTwo"))
                .AddCallHandler("h2");
            return container;

        }

        private IUnityContainer ConfigureInterceptionWithRegisterType(IUnityContainer container)
        {
            container.Configure<Interception>()
                .SetInterceptorFor<Interceptee>(null, new VirtualMethodInterceptor());
            return container;
        }
    }

    public class Interceptee
    {
        public virtual int MethodOne()
        {
            return 37;
        }

        public virtual string MethodTwo(string s, int i)
        {
            return s + ";" + i.ToString();
        }
    }

    public class GenericFactory<T> where T : class, new()
    {
        public T MakeAT()
        {
            return new T();
        }
    }

    public class SubjectOne
    {

    }

    public class SubjectTwo
    {
        public string GetAValue(string s)
        {
            return "**" + s + "**";
        }
    }

    public class NewVirtualOverrideTestClass : NewVirtualOverrideTestClassBase
    {
        public override bool TestMethod1()
        {
            return true;
        }

        public new virtual bool TestMethod2()
        {
            return true;
        }

        public override bool TestMethod4()
        {
            return true;
        }

        public new virtual void TestMethod5(int bb)
        {

        }
    }

    public abstract class NewVirtualOverrideTestClassBase
    {

        public virtual bool TestMethod1()
        {
            return false;
        }

        public virtual bool TestMethod2()
        {
            return false;
        }

        public virtual bool TestMethod3()
        {
            return true;
        }

        public abstract bool TestMethod4();

        public virtual void TestMethod5(int a)
        {

        }
    }

    public class BaseClass
    {
        public virtual string Method()
        {
            return "base";
        }
    }

    public class ImplementationOne : BaseClass
    {
        public override string Method()
        {
            return "ImplementationOne";
        }
    }

    public class ImplementationTwo : BaseClass
    {
        public override string Method()
        {
            return "ImplementationTwo";
        }
    }

    public class ClassWithVirtualProperty
    {
        public virtual int Property { get; set; }
    }

    public class ClassWithSingleNonDefaultConstructor
    {
        private string value;

        public ClassWithSingleNonDefaultConstructor(string value)
        {
            this.value = value;
        }

        public virtual string GetValue()
        {
            return value;
        }
    }

    internal class ClassWithInternalConstructor
    {
        public ClassWithInternalConstructor()
        {
        }

        public virtual int Method()
        {
            return 10;
        }
    }
}