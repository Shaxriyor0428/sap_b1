using System.Text.Json;
using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.Entities;

public class EntityHandlerTests
{
    private static readonly EntityDefinition Entity = EntityCatalog.BusinessPartners;
    private static readonly IReadOnlyList<FieldValue> Key = [new FieldValue(Entity.Key[0], "c1")];

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    private static ODataQuery EmptyQuery() => ODataQuery.Parse(Entity, new Dictionary<string, string>());

    [Fact]
    public async Task List_returns_one_page_and_reports_the_next_one()
    {
        var store = new FakeEntityStore { Rows = Enumerable.Range(0, 21).Select(_ => FullRow()).ToList() };

        var page = await new EntityHandler(store).ListAsync(Entity, EmptyQuery(), new PagePlan(null, 0, 20), default);

        Assert.Equal(21, store.LastQuery!.Limit);
        Assert.Equal(20, page.Value.Count);
        Assert.Equal(new NextPage(20, null), page.Next);
    }

    [Fact]
    public async Task Get_of_a_missing_object_returns_the_not_found_error()
    {
        var handler = new EntityHandler(new FakeEntityStore());

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            handler.GetAsync(Entity, Key, Entity.Fields, default));

        Assert.Equal(ServiceLayerErrorKind.NotFound, error.Kind);
        Assert.Equal("-2028", error.Code);
    }

    [Fact]
    public async Task Update_of_a_missing_object_returns_the_not_found_error()
    {
        var handler = new EntityHandler(new FakeEntityStore { UpdateFinds = false });

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            handler.UpdateAsync(Entity, Key, Json("""{"CardName":"x"}"""), default));

        Assert.Equal("-2028", error.Code);
    }

    [Fact]
    public async Task Update_with_only_ignored_fields_writes_nothing_but_still_checks_existence()
    {
        var store = new FakeEntityStore();
        var handler = new EntityHandler(store);

        await Assert.ThrowsAsync<ServiceLayerException>(() =>
            handler.UpdateAsync(Entity, Key, Json("""{"CurrentAccountBalance":1}"""), default));

        Assert.False(store.UpdateCalled);
    }

    [Fact]
    public async Task Delete_of_a_missing_object_returns_the_not_found_error()
    {
        var handler = new EntityHandler(new FakeEntityStore { DeleteFinds = false });

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() => handler.DeleteAsync(Entity, Key, default));

        Assert.Equal("-2028", error.Code);
    }

    [Fact]
    public async Task Create_returns_the_stored_row_and_its_url_key()
    {
        var store = new FakeEntityStore { InsertedRow = FullRow() };

        var created = await new EntityHandler(store)
            .CreateAsync(Entity, Json("""{"CardCode":"c1","CardName":"n"}"""), default);

        Assert.Equal("('c1')", created.KeyInUrl);
        Assert.Equal("c1", created.Value["CardCode"]!.GetValue<string>());
    }

    private static Dictionary<string, object?> FullRow() => new()
    {
        ["CardCode"] = "c1",
        ["CardName"] = "n",
        ["CardType"] = "C",
        ["Phone1"] = null,
        ["Cellular"] = null,
        ["E_Mail"] = null,
        ["Address"] = null,
        ["City"] = null,
        ["Balance"] = 0m,
    };

    private sealed class FakeEntityStore : IEntityStore
    {
        public List<Dictionary<string, object?>> Rows { get; init; } = [];

        public Dictionary<string, object?>? InsertedRow { get; init; }

        public bool UpdateFinds { get; init; } = true;

        public bool DeleteFinds { get; init; } = true;

        public EntityQuery? LastQuery { get; private set; }

        public bool UpdateCalled { get; private set; }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
            EntityDefinition entity,
            EntityQuery query,
            CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(Rows);
        }

        public Task<IReadOnlyDictionary<string, object?>?> FindAsync(
            EntityDefinition entity,
            IReadOnlyList<FieldValue> key,
            IReadOnlyList<FieldDefinition> columns,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, object?>?>(Rows.FirstOrDefault());

        public Task<IReadOnlyDictionary<string, object?>> InsertAsync(
            EntityDefinition entity,
            IReadOnlyList<FieldValue> values,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, object?>>(InsertedRow!);

        public Task<bool> UpdateAsync(
            EntityDefinition entity,
            IReadOnlyList<FieldValue> key,
            IReadOnlyList<FieldValue> values,
            CancellationToken cancellationToken)
        {
            UpdateCalled = true;
            return Task.FromResult(UpdateFinds);
        }

        public Task<bool> DeleteAsync(
            EntityDefinition entity,
            IReadOnlyList<FieldValue> key,
            CancellationToken cancellationToken) =>
            Task.FromResult(DeleteFinds);
    }
}
