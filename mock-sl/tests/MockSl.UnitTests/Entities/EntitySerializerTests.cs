using MockSl.Application.Entities;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.Entities;

public class EntitySerializerTests
{
    [Fact]
    public void Row_uses_service_layer_names_and_enum_names()
    {
        var entity = EntityCatalog.BusinessPartners;
        var fields = new[] { "CardType", "EmailAddress", "Phone1", "CurrentAccountBalance" }
            .Select(name => entity.FindField(name)!);
        var row = new Dictionary<string, object?>
        {
            ["CardType"] = "C",
            ["E_Mail"] = "a@b.uz",
            ["Phone1"] = null,
            ["Balance"] = 1500.500000m,
        };

        var json = EntitySerializer.ToJson(fields, row);

        Assert.Equal(
            """{"CardType":"cCustomer","EmailAddress":"a@b.uz","Phone1":null,"CurrentAccountBalance":1500.5}""",
            json.ToJsonString());
    }
}
