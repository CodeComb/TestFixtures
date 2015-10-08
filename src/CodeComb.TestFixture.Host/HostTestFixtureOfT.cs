using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CodeComb.TestFixture.Host
{
    public class HostTestFixture<TStartup> : HostTestFixture
        where TStartup : new()
    {
        public HostTestFixture()
            : base(new TStartup())
        {
        }
    }
}
