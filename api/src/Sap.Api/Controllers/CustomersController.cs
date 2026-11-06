using Microsoft.AspNetCore.Mvc;
using Sap.Api.Http;
using Sap.Application.Customers;

namespace Sap.Api.Controllers;

[ApiController]
[Route("api/customers")]
[RequireSession]
public sealed class CustomersController(CustomerService customers) : ControllerBase
{
    [HttpGet]
    public Task<CustomerPage> List(
        string? name,
        string? phone,
        string? address,
        int page = 1,
        int pageSize = CustomerService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new CustomerQuery(name, phone, address, page, pageSize);
        return customers.ListAsync(HttpContext.GetUserSession(), query, cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Create(NewCustomer customer, CancellationToken cancellationToken)
    {
        var created = await customers.CreateAsync(HttpContext.GetUserSession(), customer, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPatch("{code}")]
    public async Task<IActionResult> Update(string code, CustomerChanges changes, CancellationToken cancellationToken)
    {
        await customers.UpdateAsync(HttpContext.GetUserSession(), code, changes, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code, CancellationToken cancellationToken)
    {
        await customers.DeleteAsync(HttpContext.GetUserSession(), code, cancellationToken);
        return NoContent();
    }
}
