
# Reference Application: Practice Evolve Prime and NetDocuments Integration

## Overview

This application serves as a **reference integration** between **Practice Evolve Prime** (a web application for legal accounts management) and **NetDocuments** (a document management system). It demonstrates key capabilities for reacting to events in Prime and interacting with NetDocuments to update entities and manage document uploads.

---

## Features

### Webhook Management
- Registering webhooks for events occurring in Prime.
- Handling received webhook messages.

### API Interaction
- Fetching client, matter and document data from Prime's API.

### Tenant Configuration
- Enabling a single application instance to handle multiple tenants at once.

### Integration with NetDocuments
- Uploading documents triggered by Prime events to NetDocuments.
- Synchronizing entity updates across systems.

---

## Requirements

- **Practice Evolve Prime API access**: Ensure your Prime instance has webhooks and API features enabled.
- **NetDocuments API credentials**: Obtain valid API keys and configuration for NetDocuments.

---

## Setup

1. **Clone the Repository**
   ```bash
   git clone <repository-url>
   ```

2. **Configure Dev Environment**
   - Edit appsettings.development.json with values specific to your Prime and NetDocuments dev systems.
	- Settings are documented in appsettings.json.
	- Base Prime settings are at the top level of the config file.
	- Tenant-specific details are in the 'Tenants' array.

3. **Run the Application**
   Launch the application:
   - F5 in Visual Studio

4. **Deploy**
   Deploy into a web hosting service of your choice.
---

## Usage
1. Run app with and observe webhooks being registered in Prime.
2. Open Prime and Create/Modify Clients and Matters and add documents to Matters.
3. Observe webhook callbacks into PrimeController and commands invoked on Prime Api and NetDocuments.
4. Open NetDocuments browser and observe documents that have been added to your cabinet with correct Client and Matter attributes.
