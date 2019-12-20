public class AzResources
{
    public Value[] value { get; set; }
}

public class Value
{
    public string id { get; set; }
    public string name { get; set; }
    public string type { get; set; }
    public string location { get; set; }
    public Tags tags { get; set; }
    public Sku sku { get; set; }
    public string managedBy { get; set; }
    public string kind { get; set; }
    public Identity identity { get; set; }
}

public class Tags
{
    public string description { get; set; }
    public string createdBy { get; set; }
    public string responsible { get; set; }
    public string projectName { get; set; }
    public string companyName { get; set; }
    public string environment { get; set; }
    public string organizationUnit { get; set; }
    public string tier { get; set; }
    public string dataProfileDatasecuritylevel { get; set; }
    public string creationDate { get; set; }
    public string hiddentitle { get; set; }
    public string Createdby { get; set; }
    public string Companyname { get; set; }
    public string Tier { get; set; }
}

public class Sku
{
    public string name { get; set; }
    public string tier { get; set; }
    public int capacity { get; set; }
    public string size { get; set; }
    public string family { get; set; }
}

public class Identity
{
    public string principalId { get; set; }
    public string tenantId { get; set; }
    public string type { get; set; }
}


public class InvalidAuthTokenError
{
    public Error error { get; set; }
}

public class Error
{
    public string code { get; set; }
    public string message { get; set; }
}


public class ResourceType
{
    public string id { get; set; }
    public string _namespace { get; set; }
    public Authorization[] authorizations { get; set; }
    public Resourcetype[] resourceTypes { get; set; }
    public string registrationState { get; set; }
    public string registrationPolicy { get; set; }
}

public class Authorization
{
    public string applicationId { get; set; }
    public string roleDefinitionId { get; set; }
}

public class Resourcetype
{
    public string resourceType { get; set; }
    public string[] locations { get; set; }
    public string[] apiVersions { get; set; }
    public string capabilities { get; set; }
}
