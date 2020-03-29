using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebAssemblyDotNET
{
    [Serializable]
    public class WebAssemblyTrap : Exception
    {
        public WebAssemblyTrap(string message) : base(message)
        {

        }

        public WebAssemblyTrap(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
