using System.Diagnostics;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Management.Features.Appointments.Models;
using IIIFAuth2.Management.Infrastructure.Requests;
using IIIFAuth2.Management.Utils;
using MediatR;
using Microsoft.EntityFrameworkCore;
// ReSharper disable EntityFramework.ClientSideDbFunctionCall

namespace IIIFAuth2.Management.Features.Appointments.Requests;

/// <summary>
/// Get a page of customer's appointments matching search, most recently created first
/// </summary>
public class SearchAppointments(int customerId, AppointmentSearch search) : IRequest<PageOf<Appointment>>,
    IPagedRequest
{
    public int CustomerId { get; } = customerId;
    public AppointmentSearch Search { get; } = search;
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class SearchAppointmentsHandler(AuthServicesContext dbContext, ILogger<SearchAppointmentsHandler> logger)
    : IRequestHandler<SearchAppointments, PageOf<Appointment>>
{
    public async Task<PageOf<Appointment>> Handle(SearchAppointments request, CancellationToken cancellationToken)
    {
        var search = request.Search;
        var query = BuildSearchQuery(request, search);

        return await GetPageOfAppointments(request, query, cancellationToken);
    }

    private IQueryable<Appointment> BuildSearchQuery(SearchAppointments request, AppointmentSearch search)
    {
        var query = dbContext.Appointments.AsNoTracking().GetCustomerAppointments(request.CustomerId);

        if (!string.IsNullOrEmpty(search.Token))
        {
            var token = ContainsPattern(search.Token);
            query = query.Where(a => EF.Functions.ILike(a.Token, token));
        }

        if (!string.IsNullOrEmpty(search.Name))
        {
            var name = ContainsPattern(search.Name);
            query = query.Where(a => EF.Functions.ILike(a.Name!, name));
        }

        if (!string.IsNullOrEmpty(search.Email))
        {
            var email = ContainsPattern(search.Email);
            query = query.Where(a => EF.Functions.ILike(a.Email!, email));
        }

        if (search.Active.HasValue) query = query.Where(a => a.Active == search.Active.Value);
        if (search.StartFrom.HasValue) query = query.Where(a => a.Start >= search.StartFrom.Value.ToUtc());
        if (search.StartTo.HasValue) query = query.Where(a => a.Start <= search.StartTo.Value.ToUtc());
        if (search.EndFrom.HasValue) query = query.Where(a => a.End >= search.EndFrom.Value.ToUtc());
        if (search.EndTo.HasValue) query = query.Where(a => a.End <= search.EndTo.Value.ToUtc());
        if (search.AwaitingApproval)
        {
            var now = DateTime.UtcNow;
            query = query.Where(a => !a.Active && a.End >= now);
        }

        return query;
    }

    /// <summary>
    /// ILIKE pattern matching any value containing <paramref name="value"/>, with LIKE wildcards in value escaped
    /// </summary>
    private static string ContainsPattern(string value)
        => $"%{value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
    
    private async Task<PageOf<Appointment>> GetPageOfAppointments(SearchAppointments request,
        IQueryable<Appointment> query, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew(); 
        var total = await query.CountAsync(cancellationToken);
        var appointments = await query
            .OrderByDescending(a => a.Created)
            .ThenBy(a => a.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        sw.Stop();
        logger.LogInformation("Got {TotalResults} search results in {Elapsed}ms. Page {Page}", total,
            sw.ElapsedMilliseconds, request.Page);
        return new PageOf<Appointment>(appointments, total, request.Page, request.PageSize);
    }
}
