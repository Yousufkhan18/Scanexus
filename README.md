# Scanexus - Smart Library System

## Project Overview

Scanexus is a QR-Based Digital Library System developed for the Database System (CS-229T) course. 

What began as an automated issuing module has evolved into a fully intelligent digital library ecosystem. The platform allows students to request, receive, and return books on demand by scanning a QR code, while an integrated AI recommendation engine suggests relevant literature. Built using a microservices-inspired ASP.NET Core MVC architecture, the system verifies availability, handles digital history tracking, and automatically calculates fines. 

For administrators, the system provides enterprise-grade monitoring, an advanced analytics dashboard, and hardened security protocols to manage the entire circulation lifecycle.


## Technology Stack

### Frontend
* ASP.NET Core MVC (Razor Views)
* HTML5 & CSS3

### Backend & Logic
* ASP.NET Core Web App (Controllers & Models)
* C#
* RESTful Microservice APIs
* ngrok (Implemented to allow global cross-device access for the QR scanning logic)

### Security & Database
* JWT Authentication & Password Hashing
* Microsoft SQL Server (SSMS 2022)

## API Modules

Swagger UI available at: `https://localhost:5001/swagger`

### Core Endpoints

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| POST | `/api/library/login` | Student authentication (JWT) |
| POST | `/api/library/issue` | Issue book via QR code |
| POST | `/api/library/return` | Return a book |
| GET | `/api/library/books` | Get all books with availability |
| GET | `/api/library/dashboard/{universityId}` | Student dashboard data |
| GET | `/api/library/scan?qr={code}` | Scan QR — get book info |
| GET | `/api/library/recommendations/{id}` | Get AI book recommendations |
| GET | `/api/library/analytics` | Enterprise analytics (admin) |


## Features Implemented (Assignments 1, 2 & 3)

### Student Panel & AI Ecosystem
**Authentication:** Login using university ID with JWT security.
**QR Borrowing & Returns:** Scan QR codes via mobile device to seamlessly issue or return books, automatically updating availability and return dates.
**AI Recommendation Engine:** Provides personalized book suggestions based on borrowing history, department, semester, and current popularity.
**Dashboard & Notifications:** View issued books, track borrowing history, and receive overdue warnings or automated fine calculations.

### Advanced Analytics Dashboard (Librarian/Admin)
Librarians can monitor all circulation activities using an enterprise-grade analytics dashboard:
**Department-Wise Statistics:** Analyze metrics and circulation rates across different university departments.
**Circulation Trends:** Track the most active students, peak issuing timings, and popular books.
**Financial Reporting:** Monitor fine trends, fine collection summaries, and view defaulter lists.

### Enterprise Security & Architecture
**Security Hardening:** Implementation of JWT authentication, password hashing, SQL injection prevention, API throttling, and HTTPS.
**Comprehensive Logging System:** Every action is strictly logged for auditing purposes (LOGIN, BOOK ISSUE, BOOK RETURN, FINE PAYMENT, FAILED ATTEMPT).
**System Scalability:** Microservice APIs, and robust backup & archival systems.
**Role-Based Access Control (RBAC):** Strict permission management between students, librarians, and system administrators.


## Database Schema

The complete relational schema of the database is available in the project file.

Refer to this document for table structures, keys, relationships, and the new logging/analytics tables used in the project.



## Borrowing & Return Workflows

### Borrowing Workflow
1. Student logs in using their university ID.
2. Student accesses the application on their mobile device via the provided ngrok link.
3. Student scans the QR code provided in the book catalog.
4. Backend validates student eligibility, active status, book availability, and borrowing limits.
5. A unique transaction ID is generated, the action is logged, and the book is issued digitally.

### Return Workflow
1. Student scans the QR code to return the borrowed book.
2. The system registers the action, logs the event, and updates the return date in the database.
3. The availability status of the book is immediately changed to available.
4. The system calculates any overdue fine automatically.
5. If applicable, the student is notified of overdue warnings or fines.


## Project Scope & Deliverables

### Implemented Modules
* Database schema, SQL scripts, and API documentation
* Student authentication module and dashboard
* QR issuing and return module
* Fine system and notification engine
* AI recommendation module
* Enterprise analytics dashboard
* Role management (RBAC) and Security testing report
* Deployment guide and logging system


## Team Members

**Syeda Ayesha Rashidi** --> 2024F-BCS-057  
**Yousuf Khan** --> 2024F-BCS-106  
**Hafsa Fatima** --> 2024F-BCS-075  
**Syed Farzan Ali Anvery** --> 2024F-BCS-095

Database System (CS-229T) Assignment 3  
QR-Based Digital Library System
