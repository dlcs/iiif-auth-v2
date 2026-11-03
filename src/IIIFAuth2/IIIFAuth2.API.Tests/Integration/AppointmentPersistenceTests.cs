using IIIFAuth2.API.Tests.TestingInfrastructure;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace IIIFAuth2.API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class AppointmentPersistenceTests
{
    private readonly DatabaseFixture dbFixture;

    public AppointmentPersistenceTests(DatabaseFixture dbFixture)
    {
        this.dbFixture = dbFixture;
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Appointment_RoundTrips_WithAssetPatterns_AndSessionAppointment()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            Customer = DatabaseFixture.Customer,
            RoleProviderId = dbFixture.AppointmentRoleProviderId,
            Token = "a5rf65ne99pa",
            Roles = [DatabaseFixture.AppointmentRoleUri],
            Start = start,
            End = start.AddHours(8),
            Active = true,
            Name = "Test Person",
            AssetPatterns = new AssetPatterns
            {
                Space = [1], Scopes = ["manifest-a"], String1 = ["LIDDLE/WW2/HFIU"], Number1 = [10]
            }
        };
        var sessionUser = new SessionUser
        {
            Id = Guid.NewGuid(),
            Customer = DatabaseFixture.Customer,
            CookieId = Guid.NewGuid().ToString(),
            AccessToken = Guid.NewGuid().ToString(),
            Origin = "http://localhost",
            Expires = start.AddDays(1),
            SessionAppointments = [new SessionAppointment { AppointmentId = appointment.Id }]
        };

        await using (var writeContext = dbFixture.CreateNewAuthServiceContext())
        {
            writeContext.Appointments.Add(appointment);
            writeContext.SessionUsers.Add(sessionUser);
            await writeContext.SaveChangesAsync();
        }

        // Act
        await using var readContext = dbFixture.CreateNewAuthServiceContext();
        var fromDb = await readContext.SessionAppointments
            .Where(sa => sa.SessionUserId == sessionUser.Id)
            .Select(sa => sa.Appointment)
            .SingleAsync();

        // Assert
        fromDb.Should().BeEquivalentTo(appointment, opts => opts
            .Excluding(a => a.Created)
            .Excluding(a => a.RoleProvider));
        fromDb.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        fromDb.AssetPatterns.Tags.Should().BeNull();
    }
}
