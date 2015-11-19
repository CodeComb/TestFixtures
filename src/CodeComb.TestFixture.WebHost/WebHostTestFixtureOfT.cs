using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CodeComb.TestFixture.WebHost
{
    public class WebHostTestFixture<TStartup> : WebHostTestFixture
        where TStartup : new()
    {
        public WebHostTestFixture()
            : base(new TStartup())
        {
        }
    }
}
