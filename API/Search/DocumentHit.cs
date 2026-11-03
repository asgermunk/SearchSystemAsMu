using System;
using System.Collections.Generic;
using Shared.Model;

namespace API.Search
{
    public class DocumentHit
    {
        // parameter names match the properties, so CachedSearchLogic can deserialize it
        public DocumentHit(BEDocument document, int noOfHits, List<string> missing)
        {
            Document = document;
            NoOfHits = noOfHits;
            Missing = missing;
        }

        public BEDocument Document { get;  }

        public int NoOfHits { get;  }

        public List<string> Missing { get;  }
    }
}
