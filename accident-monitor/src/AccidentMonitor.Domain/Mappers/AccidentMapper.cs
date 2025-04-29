using AccidentMonitor.Domain.Dtos;
using AccidentMonitor.Domain.Entities.Accident;

public static class AccidentMapper
{
    public static AccidentDto ToDto(this AccidentEntity entity)
    {
        return new AccidentDto
        {
            Guid = entity.Guid, 
            Timestamp = entity.Timestamp,
            Longitude = entity.Longitude,
            Latitude = entity.Latitude,
            Severity = entity.Severity,
            ResolvedStatus = entity.ResolvedStatus,
            IsBlockingWay = entity.IsBlockingWay
        };
    }

    // Mapping từ DTO -> Entity
    public static AccidentEntity ToEntity(this AccidentDto dto)
    {
        return new AccidentEntity
        (
            timestamp: dto.Timestamp,
            longitude: (float)dto.Longitude,
            latitude: (float)dto.Latitude,
            isBlockingWay: dto.IsBlockingWay,
            severity: dto.Severity,
            resolvedStatus: dto.ResolvedStatus
        )
        {
            Guid = dto.Guid
        };
    }

    public static void UpdateEntity(this AccidentDto dto, AccidentEntity entity)
    {
        entity.Timestamp = dto.Timestamp;
        entity.Longitude = (float)dto.Longitude;
        entity.Latitude = (float)dto.Latitude;
        entity.IsBlockingWay = dto.IsBlockingWay;
        entity.Severity = dto.Severity;
        entity.ResolvedStatus = dto.ResolvedStatus;
    }
}
