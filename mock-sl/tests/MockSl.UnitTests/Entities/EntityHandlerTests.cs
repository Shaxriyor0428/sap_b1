using System.Text.Json;
using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Application.Rules;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.Entities;

public class EntityHandlerTests
{
    private static readonly EntityDefinition Entity = EntityCatalog.BusinessPartners;
    private static readonly IReadOnlyList<FieldValue> Key = [new FieldValue(Entity.Key[0], "c1")];

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    private static ODataQuery EmptyQuery() => ODataQuery.Parse(Entity, new Dictionary<string, string>());

    private static EntityHandler NewHandler(FakeEntityStore store) => new(store, new RulesCatalog(TimeProvider.System));

    [Fact]
    public async Task List_returns_one_page_and_reports_the_next_one()
    {
        var store = new FakeEntityStore { Rows = Enumerable.Range(0, 21).Select(_ => FullRow()).ToList() };

        var page = await NewHandler(store).ListAsync(Entity, EmptyQuery(), new PagePlan(null, 0, 20), default);

        Assert.Equal(21, store.LastQuery!.Limit);
        Assert.Equal(20, page.Value.Count);
        Assert.Equal(new NextPage(20, null), page.Next);
    }

    [Fact]
    public async Task Get_of_a_missing_object_returns_the_not_found_error()
    {
        var handler = NewHandler(new FakeEntityStore());

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            handler.GetAsync(Entity, Key, EmptyQuery(), default));

        Assert.Equal(ServiceLayerErrorKind.NotFound, error.Kind);
        Assert.Equal("-2028", error.Code);
    }

    [Fact]
    public async Task Update_of_a_missing_object_returns_the_not_found_error_and_saves_nothing()
    {
        var store = new FakeEntityStore { UpdateFinds = false };

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            NewHandler(store).UpdateAsync(Entity, Key, Json("""{"CardName":"x"}"""), default));

        Assert.Equal("-2028", error.Code);
        Assert.False(store.Committed);
    }

    [Fact]
    public async Task Update_commits_the_change()
    {
        var store = new FakeEntityStore();

        await NewHandler(store).UpdateAsync(Entity, Key, Json("""{"CardName":"x"}"""), default);

        Assert.True(store.Committed);
    }

    [Fact]
    public async Task Update_with_only_ignored_fields_writes_nothing_but_still_checks_existence()
    {
        var store = new FakeEntityStore();

        await Assert.ThrowsAsync<ServiceLayerException>(() =>
            NewHandler(store).UpdateAsync(Entity, Key, Json("""{"CurrentAccountBalance":1}"""), default));

        Assert.False(store.UpdateCalled);
    }

    [Fact]
    public async Task Delete_of_a_missing_object_returns_the_not_found_error()
    {
        var handler = NewHandler(new FakeEntityStore { DeleteFinds = false });

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() => handler.DeleteAsync(Entity, Key, default));

        Assert.Equal("-2028", error.Code);
    }

    [Theory]
    [InlineData("Invoices")]
    [InlineData("PurchaseInvoices")]
    [InlineData("IncomingPayments")]
    [InlineData("VendorPayments")]
    public async Task Documents_cannot_be_deleted(string setName)
    {
        var store = new FakeEntityStore();
        var entity = EntityCatalog.Find(setName)!;

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            NewHandler(store).DeleteAsync(entity, [new FieldValue(entity.Key[0], 1)], default));

        Assert.Equal("-5006", error.Code);
        Assert.Equal("The requested action is not supported for this object.", error.Message);
        Assert.False(store.SessionOpened);
    }

    [Fact]
    public async Task Object_without_actions_refuses_an_action()
    {
        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            NewHandler(new FakeEntityStore()).RunActionAsync(Entity, Key, "Cancel", default));

        Assert.Equal("-5006", error.Code);
    }

    [Theory]
    [InlineData("Invoices", "Reopen")]
    [InlineData("IncomingPayments", "Close")]
    public async Task Actions_that_are_not_built_are_refused(string setName, string action)
    {
        var entity = EntityCatalog.Find(setName)!;

        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            NewHandler(new FakeEntityStore()).RunActionAsync(entity, [new FieldValue(entity.Key[0], 1)], action, default));

        Assert.Equal("-5006", error.Code);
    }

    [Fact]
    public async Task Create_returns_the_stored_row_and_its_url_key_and_commits()
    {
        var store = new FakeEntityStore { InsertedRow = FullRow() };

        var created = await NewHandler(store).CreateAsync(Entity, Json("""{"CardCode":"c1","CardName":"n"}"""), default);

        Assert.Equal("('c1')", created.KeyInUrl);
        Assert.Equal("c1", created.Value["CardCode"]!.GetValue<string>());
        Assert.True(store.Committed);
    }

    [Fact]
    public async Task Failed_create_saves_nothing()
    {
        var store = new FakeEntityStore { InsertedRow = FullRow() };

        await Assert.ThrowsAsync<ServiceLayerException>(() =>
            NewHandler(store).CreateAsync(Entity, Json("""{"CardName":"no code"}"""), default));

        Assert.False(store.Committed);
    }

    private static EntityRow FullRow() => new(new Dictionary<string, object?>
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
    });

    private sealed class FakeEntityStore : IEntityStore, IEntitySession
    {
        public List<EntityRow> Rows { get; init; } = [];

        public EntityRow? InsertedRow { get; init; }

        public bool UpdateFinds { get; init; } = true;

        public bool DeleteFinds { get; init; } = true;

        public EntityQuery? LastQuery { get; private set; }

        public bool UpdateCalled { get; private set; }

        public bool Committed { get; private set; }

        public bool SessionOpened { get; private set; }

        public Task<IEntitySession> OpenSessionAsync(CancellationToken cancellationToken)
        {
            SessionOpened = true;
            return Task.FromResult<IEntitySession>(this);
        }

        public Task<IReadOnlyList<EntityRow>> QueryAsync(EntityDefinition entity, EntityQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<EntityRow>>(Rows);
        }

        public Task<EntityRow?> FindAsync(
            EntityDefinition entity,
            IReadOnlyList<FieldValue> key,
            IReadOnlyList<FieldDefinition> columns,
            CancellationToken cancellationToken) =>
            Task.FromResult(Rows.FirstOrDefault());

        public Task<EntityRow?> FindForUpdateAsync(EntityDefinition entity, IReadOnlyList<FieldValue> key, CancellationToken cancellationToken) =>
            Task.FromResult(Rows.FirstOrDefault());

        public Task<EntityRow> InsertAsync(EntityDefinition entity, IReadOnlyList<FieldValue> values, CancellationToken cancellationToken) =>
            Task.FromResult(InsertedRow!);

        public Task<bool> UpdateAsync(
            EntityDefinition entity,
            IReadOnlyList<FieldValue> key,
            IReadOnlyList<FieldValue> values,
            CancellationToken cancellationToken)
        {
            UpdateCalled = true;
            return Task.FromResult(UpdateFinds);
        }

        public Task<bool> DeleteAsync(EntityDefinition entity, IReadOnlyList<FieldValue> key, CancellationToken cancellationToken) =>
            Task.FromResult(DeleteFinds);

        public Task AddToNumberAsync(
            EntityDefinition entity,
            IReadOnlyList<FieldValue> key,
            FieldDefinition field,
            decimal amount,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<EntityRow>> QueryChildrenAsync(
            ChildDefinition child,
            IReadOnlyList<object?> parentKeys,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EntityRow>>([]);

        public Task InsertChildAsync(
            ChildDefinition child,
            object? parentKey,
            IReadOnlyList<FieldValue> values,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateChildAsync(
            ChildDefinition child,
            object? parentKey,
            object? rowKey,
            IReadOnlyList<FieldValue> values,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateAllChildrenAsync(
            ChildDefinition child,
            object? parentKey,
            IReadOnlyList<FieldValue> values,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
