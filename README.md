PeasyWare WMS

A modern, professional Warehouse Management System built from the ground up to showcase best practices in WMS design and software engineering.
About The Project

PeasyWare WMS is a personal and professional journey into the development of a modern Warehouse Management System. The goal is not to create a commercial product, but rather to build a comprehensive, robust, and realistic WMS that demonstrates a deep understanding of warehouse operations, from inbound receiving to outbound shipping.

The system is designed based on years of real-world experience with industry-leading WMS platforms like SAP EWM, JDA/Blue Yonder, and Manhattan, incorporating their best features and practices into a clean, modern architecture.
Built With

    Backend & Application Logic: C# with .NET

    Database: Microsoft SQL Server

    User Interfaces:

        A .NET Console Application for shop-floor operations (current focus).

        A Windows Desktop Application (WPF or WinForms) for administrative tasks (future development).

Project Structure (Monorepo)

This project is structured as a monorepo to keep all related components of the PeasyWare ecosystem in a single, easy-to-manage repository.

    /DataBase/: Contains all SQL scripts for creating the database schema, procedures, views, and test data.

    /src/PeasyWare.WMS.Console/: The source code for the .NET console application used by warehouse operators.

    /src/PeasyWare.WMS.Desktop/: (Placeholder) For the future administrative desktop application.

    /docs/: Contains project documentation, such as flowcharts and design notes.

Getting Started

To get a local copy up and running, follow these simple steps.
Prerequisites

    Microsoft SQL Server: An instance of SQL Server (2019 or later, including the free Express or Developer editions) is required.

    .NET SDK: The .NET SDK (version 8.0 or later) is needed to build and run the console application.

    Git: To clone the repository.

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

        This will create the WMS_DB database, all necessary tables and procedures, and populate it with test users and data.

    Configure the Connection String:

        Navigate to the /src/PeasyWare.WMS.Console/ folder.

        Open the appsettings.json file.

        Update the Server property in the DefaultConnection string to point to your SQL Server instance (e.g., localhost, SQLEXPRESS, or a server IP address).

    Build and Run the Application:

        Open a terminal in the /src/PeasyWare.WMS.Console/ directory.

        Run the following commands:

        dotnet build
        dotnet run

        You can log in with one of the test users (e.g., username: admin, password: admin).

Current Features (Console App v0.4)

The console application currently supports the following core warehouse functions:

    Secure User Login: With different roles and permissions.

    Stock Query: Allows an operator to scan a pallet ID and view its detailed information (material, status, location, etc.).

    Bin Query: Allows an operator to scan a location and view its contents, including an aggregated summary for multi-pallet locations and a detailed drill-down view.

    Guided Putaway: A complete, end-to-end workflow for putting away stock, featuring:

        Intelligent location suggestions based on product type, section preference, and aisle workload.

        Robust validation for pallets and locations.

        Transactional safety for all database operations.

        The ability to Cancel an in-progress task.

        The ability to Modify a system suggestion to a different, manually scanned location.

Project Roadmap

The following features are planned for future development:

    [ ] Bin to Bin Movement: For stock consolidation and manual moves.

    [ ] Cycle Counting: To ensure inventory accuracy.

    [ ] Receiving Module: To book stock into the warehouse against an ASN.

    [ ] Picking & Allocation Module: To fulfill customer orders.

    [ ] Shipping Module: To manage dispatched goods.

    [ ] Desktop Admin Application: A GUI for managing master data, users, and system settings.
