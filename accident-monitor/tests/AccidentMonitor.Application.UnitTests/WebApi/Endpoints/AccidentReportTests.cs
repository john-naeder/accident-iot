using AccidentMonitor.Application.Accident.Commands.CreateAccident;
using AccidentMonitor.Application.Accident.Commands.UpdateResolveStatus;
using AccidentMonitor.Domain.Entities.Accident;
using AccidentMonitor.Domain.Enums;
using AccidentMonitor.WebApi.Endpoints;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using NUnit.Framework;

namespace AccidentMonitor.Application.UnitTests.WebApi.Endpoints;

[TestFixture]
public class AccidentReportTests
{
    private Mock<ISender> _mockSender;
    private AccidentReport _endpoint;

    [SetUp]
    public void SetUp()
    {
        _mockSender = new Mock<ISender>();
        _endpoint = new AccidentReport();
    }

    [Test]
    public async Task ReportAccident_ValidCommand_ReturnsCreatedResult()
    {
        // Arrange
        var command = new CreateAccidentCommand
        {
            Latitude = 10.0,
            Longitude = 106.0,
            IsBlockingWay = true,
            Time = DateTime.UtcNow,
            Severity = AccidentSeverity.Minor
        };
        
        var expectedId = Guid.NewGuid();
        _mockSender.Setup(s => s.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);

        // Act
        var result = await _endpoint.ReportAccident(_mockSender.Object, command);

        // Assert
        result.Should().BeOfType<Created<Guid>>();
        result.Value.Should().Be(expectedId);
        result.Location.Should().Be($"/{nameof(AccidentEntity)}/{expectedId}");
        _mockSender.Verify(s => s.Send(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAccidentStatus_ExistingAccident_ReturnsNoContent()
    {
        // Arrange
        var accidentId = Guid.NewGuid();
        var command = new UpdateAccidentResolvedStatusCommand
        {
            AccidentId = Guid.Empty, // This will be overridden in the endpoint
            IsResolved = true
        };
        
        // The endpoint modifies the command so we need to capture it
        UpdateAccidentResolvedStatusCommand capturedCommand = null;
        _mockSender.Setup(s => s.Send(It.IsAny<UpdateAccidentResolvedStatusCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Guid?>, CancellationToken>((c, _) => capturedCommand = (UpdateAccidentResolvedStatusCommand)c)
            .ReturnsAsync(accidentId);

        // Act
        var result = await _endpoint.UpdateAccidentStatus(_mockSender.Object, accidentId, command);

        // Assert
        result.Result.Should().BeOfType<NoContent>();
        capturedCommand.Should().NotBeNull();
        capturedCommand.AccidentId.Should().Be(accidentId);
        capturedCommand.IsResolved.Should().BeTrue();
        _mockSender.Verify(s => s.Send(It.IsAny<UpdateAccidentResolvedStatusCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAccidentStatus_NonExistingAccident_ReturnsBadRequest()
    {
        // Arrange
        var accidentId = Guid.NewGuid();
        var command = new UpdateAccidentResolvedStatusCommand
        {
            AccidentId = Guid.Empty,
            IsResolved = true
        };
        
        _mockSender.Setup(s => s.Send(It.IsAny<UpdateAccidentResolvedStatusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.Empty); // Repository returns Guid.Empty when accident not found

        // Act
        var result = await _endpoint.UpdateAccidentStatus(_mockSender.Object, accidentId, command);

        // Assert
        result.Result.Should().BeOfType<BadRequest<string>>();
        var badRequest = (BadRequest<string>)result.Result;
        badRequest.Value.Should().Be("Accident not found.");
        _mockSender.Verify(s => s.Send(It.IsAny<UpdateAccidentResolvedStatusCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
