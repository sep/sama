using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class GroupMappingSyncServiceTests
{
    [TestMethod]
    public void ExtractCnFromDnShouldParseCnFromFullDn()
    {
        var result = GroupMappingSyncService.ExtractCnFromDn("CN=Developers,OU=Groups,DC=example,DC=com");

        Assert.AreEqual("Developers", result);
    }

    [TestMethod]
    public void ExtractCnFromDnShouldReturnNullForNonCnDn()
    {
        var result = GroupMappingSyncService.ExtractCnFromDn("OU=Groups,DC=example,DC=com");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ExtractCnFromDnShouldReturnNullForEmptyOrWhitespace()
    {
        Assert.IsNull(GroupMappingSyncService.ExtractCnFromDn(""));
        Assert.IsNull(GroupMappingSyncService.ExtractCnFromDn("  "));
    }

    [TestMethod]
    public void ExtractCnFromDnShouldHandleCnOnly()
    {
        var result = GroupMappingSyncService.ExtractCnFromDn("CN=Admins");

        Assert.AreEqual("Admins", result);
    }

    [TestMethod]
    public void ExtractCnFromDnShouldBeCaseInsensitive()
    {
        var result = GroupMappingSyncService.ExtractCnFromDn("cn=DevOps,OU=Groups,DC=example,DC=com");

        Assert.AreEqual("DevOps", result);
    }
}
