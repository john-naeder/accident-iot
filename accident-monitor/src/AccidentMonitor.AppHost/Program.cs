var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sqlServer")
    .WithDataVolume("accident_monitor_data");


var accidentsRecordDb = sqlServer.AddDatabase("AccidentMonitorDb");

var mqtt = builder.AddDockerfile("mqtt", "mosquitto/")
    .WithEndpoint(
        port: 1884,
        targetPort: 1883,
        scheme: "tcp",
        name: "mqttBroker",
        env: "MQTT_SERVER_PORT",
        isProxied: true)
    .WithBindMount("./mosquitto/.", "/mosquitto/.");


builder.AddProject<Projects.AccidentMonitor_WebApi>("accident-monitor-api")
    .WithReference(accidentsRecordDb)
    .WaitFor(accidentsRecordDb);

builder.Build().Run();
