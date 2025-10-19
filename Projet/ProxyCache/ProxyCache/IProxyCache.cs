using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace ProxyCache
{

    [ServiceContract]
    public interface IProxyCache
    {
        [OperationContract]
        string Get(string url);

        [OperationContract]
        string GetWithTtl(string url, double seconds);

    } 
}

