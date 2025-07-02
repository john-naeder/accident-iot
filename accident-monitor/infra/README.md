# Accident Monitor Infrastructure

This folder contains the infrastructure as code (IaC) for deploying the Accident Monitor application to Azure. The deployment is based on Azure Developer CLI (azd) and uses Bicep templates for resource provisioning.

## Architecture

The application is deployed with the following Azure resources:

- **Azure App Service**: Hosts the ASP.NET Core Web API
- **Azure SQL Database**: Stores relational data for the application
- **Azure Key Vault**: Securely stores secrets and connection strings
- **Application Insights**: Provides monitoring and telemetry
- **Log Analytics Workspace**: Centralizes logs for analysis

## Prerequisites

To deploy this infrastructure, you need:

1. **Azure CLI**: Latest version
2. **Azure Developer CLI (azd)**: Latest version
3. **Azure subscription**: With sufficient permissions to create resources

## Deployment

### Using Azure Developer CLI (Recommended)

1. Log in to Azure

   ```bash
   az login
   ```

2. Navigate to the accident-monitor folder (where azure.yaml is located)

   ```bash
   cd accident-monitor
   ```

3. Initialize the azd environment (first time only)

   ```bash
   azd init
   ```

4. Deploy the application

   ```bash
   azd up
   ```

5. If you want to deploy to a specific environment

   ```bash
   azd env select <environment-name>
   azd up
   ```

### Manual Azure CLI Deployment

If you prefer to deploy manually without azd:

1. Set environment variables

   ```bash
   $RESOURCE_GROUP = "rg-accident-monitor"
   $LOCATION = "eastus"
   $ENVIRONMENT_NAME = "dev"
   ```

2. Create a resource group

   ```bash
   az group create --name $RESOURCE_GROUP --location $LOCATION
   ```

3. Deploy the bicep template

   ```bash
   az deployment sub create \
     --location $LOCATION \
     --template-file ./infra/main.bicep \
     --parameters environmentName=$ENVIRONMENT_NAME \
     --parameters location=$LOCATION
   ```

## Post-Deployment

After deployment:

1. Configure CORS settings for your API if needed
2. Set up any required application settings through the Azure Portal or via CLI
3. Verify that the database has been properly initialized

## Troubleshooting

Common issues:

- **Resource Name Conflicts**: Add unique suffixes or choose a different environment name
- **Permission Issues**: Ensure your account has sufficient permissions in the subscription
- **Deployment Failures**: Check the error logs in the Azure Portal or CLI output
