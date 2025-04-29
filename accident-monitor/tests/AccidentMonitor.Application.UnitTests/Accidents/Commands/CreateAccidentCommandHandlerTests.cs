// File: tests/AccidentMonitor.Application.UnitTests/Accidents/Commands/CreateAccidentCommandHandlerTests.cs
using Moq;
using NUnit.Framework;
using AccidentMonitor.Application.Common.Interfaces;
using AccidentMonitor.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper; 
using FluentAssertions;
using AccidentMonitor.Domain.Entities.Accident;
using AccidentMonitor.Application.Accident.Commands.CreateAccident;

namespace AccidentMonitor.Application.UnitTests.Accidents.Commands;

[TestFixture]
public class CreateAccidentCommandHandlerTests
{
    private Mock<IUnitOfWork> _mockUnitOfWork;
    private Mock<IAccidentRepository> _mockAccidentRepo;
    private Mock<IMapper> _mockMapper;
    private CreateAccidentCommandHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _mockAccidentRepo = new Mock<IAccidentRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockUnitOfWork.Setup(u => u.AccidentRepository).Returns(_mockAccidentRepo.Object);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mockMapper = new Mock<IMapper>();
        _mockMapper.Setup(m => m.Map<AccidentEntity>(It.IsAny<CreateAccidentCommand>()))
                   .Returns(new AccidentEntity { 
                    
                    });


        _handler = new CreateAccidentCommandHandler(_mockUnitOfWork.Object); 
    }

    [Test]
    public async Task Handle_ValidCommand_ShouldCreateAccidentAndSaveChanges()
    {
        // Arrange
        var command = new CreateAccidentCommand
        {
            Latitude = 10.0,
            Longitude = 106.0,
            IsBlockingWay = true,
            Time = DateTime.UtcNow,
        };
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        _mockMapper.Verify(m => m.Map<AccidentEntity>(command), Times.Once); 
        _mockAccidentRepo.Verify(r => r.AddAsync(It.IsAny<AccidentEntity>(), cancellationToken), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(cancellationToken), Times.Once);
        result.Should();
    }

}
