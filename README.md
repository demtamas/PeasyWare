PeasyWare WMS

A professional Warehouse Management System (WMS) built from the ground up in C# and SQL Server to demonstrate best practices in logistics software, designed with the operational needs of a 3rd party logistics (3PL) provider in mind.
About The Project

PeasyWare WMS is a portfolio project demonstrating a professional, full-featured Warehouse Management System. The goal is to showcase a deep understanding of the complex logic, data structures, and operational workflows required to manage a modern warehouse efficiently.

The system is built around a core stock management module that serves as the single source of truth for all inventory. This backend database is designed to be the central engine for multiple front-end applications, including the current console app, a future desktop admin client, and potential API services.
Core Architectural Principles

    Database as the Core: All critical business logic, validation, and transactions are handled by robust T-SQL stored procedures, ensuring data integrity and high performance.

    Separation of Concerns: The C# application is built on a clean architecture that separates data access, business services, and user interface logic.

    Flexibility: The system is designed to handle a variety of operational scenarios, including fully pre-advised (ASN/EDI) and blind receiving workflows.

    Auditability: Every significant action, from login attempts to inventory movements, is logged for full traceability.

Built With

    Backend & Application Logic: C# with .NET

    Database: Microsoft SQL Server

    Logging: Serilog

Current Features (Console App)

The console application provides warehouse operators with a clean, efficient, and robust interface for performing core warehouse tasks.
Guided Putaway

A complete, end-to-end workflow for putting away stock, featuring:

    Intelligent location suggestions based on SKU-level storage type and section preferences.

    Workload balancing across aisles to reduce operator congestion.

    Robust validation for pallets, locations, and operational status.

    Full support for manual overrides, cancellations, and task resumption.

Stock & Bin Inquiry

Core lookup functions that provide real-time visibility into the warehouse's inventory.

    Stock Query: View detailed information for a single pallet, including its status, location, quantity, and batch information.

    Bin Query: View the contents of any location, with an aggregated summary for multi-pallet bins and a detailed drill-down view.

Stock Query
	

Bin Query (Aggregated)


	


Inbound & Movement

    Inbound Activation: A quality gate to activate inbound deliveries before receiving can begin.

    Receiving: A flexible receiving module that supports fully advised deliveries, allowing operators to verify and amend details (quantity, batch, BBE) before confirming receipt.

    Bin to Bin Movement: A robust function for ad-hoc stock movements, essential for consolidation and manual putaway overrides.

Getting Started

To get a local copy up and running, follow these simple steps.
Prerequisites

    Microsoft SQL Server: An instance of SQL Server (2019 or later, including the free Express or Developer editions) is required.

    .NET SDK: The .NET SDK (version 8.0 or later) is needed to build and run the console application.

Installation & Setup

    Clone the repository:

    git clone https://github.com/demtamas/PeasyWare.git

    Create the Database:

        Open the /DataBase/Scripts/ folder.

        Execute the SQL scripts on your SQL Server instance in the following order:

            01_Objects.sql

            02_Procedures.sql

            03_Views.sql

            04_Test_Data.sql

        This will create the WMS_DB database, all necessary objects, and populate it with test data.

    Configure the Connection String:

        Navigate to the /PeasyWare.WMS.Console/ folder.

        Open the appsettings.json file.

        Update the Server property in the DefaultConnection string to point to your SQL Server instance.

    Build and Run the Application:

        Open a terminal in the /PeasyWare.WMS.Console/ directory.

        Run the following commands:

        dotnet build
        dotnet run

        You can log in with one of the test users (e.g., username: admin, password: admin).

Project Roadmap

The current console application provides a solid foundation. Future development will focus on expanding the feature set and building out new interfaces.

    [ ] Cycle Counting: Implement a cycle counting module to ensure inventory accuracy.

    [ ] Outbound Flow: Design and build the picking, allocation, and shipping modules.

    [ ] Desktop Admin Application: Create a GUI for managing master data, users, system settings, and viewing reports.

    [ ] API Services: Develop a Web API to allow other systems to query stock or import delivery notifications.