using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Management.Features.Appointments.Models;
using IIIFAuth2.Management.Features.Appointments.Requests;
using IIIFAuth2.Management.Infrastructure.Hydra;
using IIIFAuth2.Management.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.Management.Features.Appointments;

/// <summary>
/// Management of customer appointments
/// </summary>
[ApiController]
[Route("customers/{customerId:int}/auth/appointments")]
public class AppointmentsController(IMediator mediator, IOptions<ManagementSettings> settings)
    : HydraController(mediator, settings)
{
    /// <summary>
    /// Search appointments, paged with ?page and ?pageSize (defaults from DefaultPageSize and MaxPageSize settings)
    /// </summary>
    [HttpGet]
    public Task<IActionResult> Search(int customerId, [FromQuery] AppointmentSearch search,
        CancellationToken cancellationToken)
        => HandlePagedFetch(new SearchAppointments(customerId, search), ToHydra, cancellationToken);

    [HttpPost]
    public Task<IActionResult> Create(int customerId, [FromBody] AppointmentRequest request,
        CancellationToken cancellationToken)
        => HandleUpsert(new CreateAppointment(customerId, request), ToHydra, "Create appointment failed",
            cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(int customerId, Guid id, CancellationToken cancellationToken)
        => HandleFetch(new GetAppointment(customerId, id), ToHydra, cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Put(int customerId, Guid id, [FromBody] AppointmentRequest request,
        CancellationToken cancellationToken)
        => HandleUpsert(new UpdateAppointment(customerId, id, request, false), ToHydra,
            "Update appointment failed", cancellationToken);

    [HttpPatch("{id:guid}")]
    public Task<IActionResult> Patch(int customerId, Guid id, [FromBody] AppointmentPatch request,
        CancellationToken cancellationToken)
        => HandleUpsert(new UpdateAppointment(customerId, id, request, true), ToHydra,
            "Update appointment failed", cancellationToken);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(int customerId, Guid id, CancellationToken cancellationToken)
        => HandleDelete(new DeleteAppointment(customerId, id), cancellationToken);

    private HydraAppointment ToHydra(Appointment appointment) => appointment.ToHydra(BaseUrl);
}
