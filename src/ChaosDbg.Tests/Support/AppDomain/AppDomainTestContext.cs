using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace ChaosDbg.Tests
{
    class AppDomainTestContext : Microsoft.VisualStudio.TestTools.UnitTesting.TestContext
    {
        public override IDictionary Properties { get; }
        public override DataRow DataRow { get; }
        public override DbConnection DataConnection { get; }

        public AppDomainTestContext(string testName)
        {
            Properties = new Dictionary<string, string>
            {
                { nameof(TestName), testName }
            };
        }

        public override void AddResultFile(string fileName)
        {
            throw new NotImplementedException();
        }

        public override void BeginTimer(string timerName)
        {
            throw new NotImplementedException();
        }

        public override void EndTimer(string timerName)
        {
            throw new NotImplementedException();
        }

        public override void Write(string message)
        {
            throw new NotImplementedException();
        }

        public override void Write(string format, params object[] args)
        {
            throw new NotImplementedException();
        }

        public override void WriteLine(string message)
        {
            throw new NotImplementedException();
        }

        public override void WriteLine(string format, params object[] args)
        {
            throw new NotImplementedException();
        }
    }
}
