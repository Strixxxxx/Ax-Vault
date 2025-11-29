# Project Overview

This is a .NET MAUI desktop application with an ASP.NET Core Web API backend. The application, named "Ax Vault", appears to be a password manager. The frontend is built with .NET MAUI and the backend is an ASP.NET Core Web API that connects to a MSSQL database.

**Frontend:**
- .NET MAUI
- C#
- XAML

**Backend:**
- ASP.NET Core Web API
- C#
- MSSQL Server
- Raw ADO.NET for database access

The application uses a multi-tenant database architecture where each user has their own database for storing their accounts. A master database is used to store user information and the name of their corresponding database.

# Building and Running

## Backend

To run the backend, you will need to have the .NET SDK installed.

1.  Navigate to the `Backend` directory.
2.  Restore the dependencies: `dotnet restore`
3.  Run the application: `dotnet run`

The API will be available at `http://localhost:5180`.

## Frontend

To run the frontend, you will need to have the .NET MAUI workload installed.

1.  Navigate to the `Frontend` directory.
2.  Restore the dependencies: `dotnet restore`
3.  Run the application on the desired platform (e.g., Windows): `dotnet build -t:Run -f net10.0-windows10.0.19041.0`

# Development Conventions

- The backend uses raw SQL queries with `SqlConnection` for database access, instead of an ORM like Entity Framework Core for the main `Account` data.
- The frontend uses `HttpClient` to communicate with the backend API.
- The code is organized into separate projects for the frontend and backend.
- The frontend uses a `Components` directory to store its UI components.
- The backend uses a standard ASP.NET Core Web API project structure.

##  Gemini Interaction Rules
-   **Rule1:** Gemini CLI must adhere to a prioritized workflow for development tasks. The order of operations is as follows: 1st - Database, 2nd - Backend, 3rd - Frontend, and 4th - Polishing (e.g., addressing minor errors, UI/UX adjustments).
-   **Rule2:** Gemini CLI must make a plan that the user would read before implementing the change in the code structure.
-   **Rule3:** Gemini CLI must properly understand the user's prompt to create a proper plan that was mentioned in Rule 1.
-   **Rule4:** The Gemini CLI must always ask if the Gemini CLI would proceed to the changes.
-   **Rule5:** Gemini CLI must provide production-ready code to make sure that nothing will happen when the system is deployed.
-   **Rule6:** When Gemini CLI must change something on the database, instead of adding a query that would require the user to be executed, it would only provide a query that can be followed by the user.
-   **Rule7:** The Gemini CLI must put all SQL-related queries inside of Query.txt.
-   **Rule8:** The user will always do the database-related changes manually instead of using Gemini CLI.
-   **Rule9:** The user would always send a feedback prompt every time that the Gemini CLI changed something in the code.
-   **Rule10:** Gemini CLI must always read the user's feedback and formulate a plan if the user's feedback has some error from the code changes that Gemini CLI did.
-   **Rule11:** Gemini CLI must always identify the mistake faster to avoid repetitive prompts to fix the error.
-   **Rule12:** Gemini CLI must always scan all relevant files in the prompt since most of the files are always updated instead of reading in memory.
-   **Rule13:** Gemini CLI must be precise & concise when it comes to plan details, code structure, and resourcefulness to make the work easier.
-   **Rule14:** Gemini CLI must learn from its previous mistakes to avoid repeating them.
-   **Rule15:** The Gemini CLI must always follow the rules that were dictated above for faster, efficient, and collaborative work with the user.


##  Gemini Interaction Rules on the Frontend Directory
- **Rule1:** When Gemini CLI changes something on the frontend, Gemini CLI would always take note that the frontend programming language is .NET MAUI.
- **Rule2:** Gemini CLI must make sure that the code changes won't cause an error when the user runs the build command. Gemini CLI must run the command 'dotnet build' automatically without the user's permission to make sure nothing will be left behind once the changes in the frontend are done.
- **Rule3:** Gemini CLI must make sure that the newly created & existing CS files must be properly connected to the backend connections.
- **Rule4:** Gemini CLI must make sure that the UI of the .NET MAUI would be Compatible on Desktop & Mobile Devices. No need to run the build command here.
- **Rule4:** Gemini CLI must adhere to the rules mentioned above to make sure nothing wrong happens to the frontend directory.

##  Gemini Interaction Rules on the Backend Directory
- **Rule1:** When Gemini CLI changes something on the backend, Gemini CLI would always take note that the backend programming language is ASP.NET Core Web API.
- **Rule2:** Gemini CLI must make sure that the routers is added to the Program.cs file if needed and has conditionally applied middleware and error logging if it's invalid.
- **Rule3:** Gemini CLI must run the build command every time there is a change in the backend directory. This rule is to make sure that there won't be any errors once this project is at the production level.
- **Rule4:** Gemini CLI must adhere to the rules mentioned above to make sure nothing wrong happens to the backend directory.