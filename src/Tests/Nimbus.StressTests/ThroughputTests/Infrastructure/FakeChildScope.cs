using System;
using Nimbus.DependencyResolution;
using Nimbus.StressTests.ThroughputTests.EventHandlers;

namespace Nimbus.StressTests.ThroughputTests.Infrastructure
{
    public class FakeChildScope : IDependencyResolverScope
    {
        private readonly FakeHandler _fakeHandler;

        public FakeChildScope(FakeHandler fakeHandler)
        {
            _fakeHandler = fakeHandler;
        }

        public IDependencyResolverScope CreateChildScope()
        {
            return new FakeChildScope(_fakeHandler);
        }

        public TComponent Resolve<TComponent>()
        {
            return (TComponent) (object) _fakeHandler;
        }

        public object Resolve(Type componentType)
        {
            throw new NotImplementedException();
        }

        public TComponent[] ResolveAll<TComponent>()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}