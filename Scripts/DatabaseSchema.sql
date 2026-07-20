USE master;
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = 'LibraryDB')
    DROP DATABASE LibraryDB;
GO

CREATE DATABASE LibraryDB;
GO

USE LibraryDB;
GO

CREATE TABLE Students (
    StudentID    INT PRIMARY KEY IDENTITY(1,1),
    UniversityID VARCHAR(20)  NOT NULL UNIQUE,
    FullName     VARCHAR(100) NOT NULL,
    FatherName   VARCHAR(100) NOT NULL,
    Email        VARCHAR(100) NOT NULL,
    PasswordHash VARCHAR(100) NOT NULL,
    Semester     INT NOT NULL,
    Batch        VARCHAR(20)  NOT NULL,
    IsActive     BIT NOT NULL DEFAULT 1,
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE()
);
GO
Select * FROM Students

CREATE TABLE Admins (
    AdminID VARCHAR(20) NOT NULL UNIQUE,
    PasswordHash VARCHAR(256) NOT NULL,
    Email VARCHAR(100),
);

INSERT INTO Admins (AdminID, PasswordHash, Email)
VALUES ( 'ADMIN-001', 'Admin@123', 'admin@scanexus.com');

SELECT * FROM Admins;

CREATE TABLE Books (
    BookID          INT PRIMARY KEY IDENTITY(1,1),
    ISBN            VARCHAR(20)  NOT NULL,
    Title           VARCHAR(200) NOT NULL,
    Author          VARCHAR(100) NOT NULL,
    Publisher       VARCHAR(150),
    TotalCopies     INT NOT NULL DEFAULT 1,
    AvailableCopies INT NOT NULL DEFAULT 1,
    QRCode          VARCHAR(200) NOT NULL UNIQUE,
    AddedAt         DATETIME NOT NULL DEFAULT GETDATE()
);
GO

CREATE TABLE Transactions (
    TransactionID INT PRIMARY KEY IDENTITY(1,1),
    TxnCode       VARCHAR(50)  NOT NULL UNIQUE,
    StudentID     INT NOT NULL FOREIGN KEY REFERENCES Students(StudentID),
    BookID        INT NOT NULL FOREIGN KEY REFERENCES Books(BookID),
    IssueDate     DATETIME NOT NULL DEFAULT GETDATE(),
    DueDate       DATETIME NOT NULL,
    ReturnDate    DATETIME NULL,
    Status        VARCHAR(20) NOT NULL DEFAULT 'Active',
    QRScanData    VARCHAR(200) NOT NULL DEFAULT ''
);
GO

USE LibraryDB;

CREATE TABLE ActivityLogs (
    LogID INT PRIMARY KEY IDENTITY(1,1),
    ActionType VARCHAR(50) NOT NULL,   
    UserID VARCHAR(20) NULL,            
    UserName VARCHAR(100) NULL,
    Details VARCHAR(500) NULL,          
    IPAddress VARCHAR(50) NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE()
);


INSERT INTO Students (UniversityID, FullName, FatherName, Email, PasswordHash, Semester, Batch, IsActive)
VALUES
('2024F-BS-0001', 'Ali Hassan',  'Hassan Khan', 'ali@ssuet.edu.pk',  'Pass@123', 4, '2024F', 1),
('2024F-BS-0002', 'Sara Ahmed',  'Ahmed Raza',  'sara@ssuet.edu.pk', 'Pass@123', 4, '2024F', 1),
('2024F-BS-0003', 'Usman Tariq', 'Tariq Mehmood','usman@ssuet.edu.pk','Pass@123', 4, '2024F', 0);
GO

INSERT INTO Books (ISBN, Title, Author, Publisher, TotalCopies, AvailableCopies, QRCode)
VALUES
('978-0-13-4685', 'Database System Concepts', 'Silberschatz', 'McGraw-Hill', 3, 3, 'BOOK-001'),
('978-0-13-1103', 'C Programming Language',   'Kernighan',    'Prentice Hall', 2, 2, 'BOOK-002'),
('978-0-20-1633', 'Design Patterns',           'Gang of Four', 'Addison-Wesley', 2, 2, 'BOOK-003'),
('978-0-13-2350', 'Computer Networking',       'Kurose Ross',  'Pearson', 4, 4, 'BOOK-004');
GO

USE LibraryDB;
ALTER TABLE Transactions ADD Remarks VARCHAR(300) NULL;


USE LibraryDB;
UPDATE Books SET QRCode = 'SSUET-LIB-BOOK-001' WHERE BookID = 1;
UPDATE Books SET QRCode = 'SSUET-LIB-BOOK-002' WHERE BookID = 2;
UPDATE Books SET QRCode = 'SSUET-LIB-BOOK-003' WHERE BookID = 3;
UPDATE Books SET QRCode = 'SSUET-LIB-BOOK-004' WHERE BookID = 4;


UPDATE Students SET IsActive = 1 WHERE UniversityID = '2024F-BS-0003';


ALTER TABLE Transactions ADD Remarks VARCHAR(300) NULL;

USE LibraryDB;
ALTER TABLE Transactions 
ADD FineAmount DECIMAL(10,2) NOT NULL DEFAULT 0,
    FinePaid BIT NOT NULL DEFAULT 0;


USE LibraryDB;

UPDATE Students 
SET PasswordHash = 'B6BC7B58510319A151D168BA3D5AECB3AC0A9708D06DD930F37FBC89B6CDC697';

UPDATE Admins 
SET PasswordHash = 'E86F78A8A3CAF0B60D8E74E5942AA6D86DC150CD3C03338AEF25B7D2D7E3ACC7';

ALTER TABLE Students 
ADD Department NVARCHAR(100) NOT NULL DEFAULT 'Computer Science';

UPDATE Students 
SET Department = 'Computer Science' 
WHERE Batch = '2024F';

Update Students SET UniversityID = '2023S-IT-0001' WHERE StudentID = 4

UPDATE Students 
SET PasswordHash = 'B6BC7B58510319A151D168BA3D5AECB3AC0A9708D06DD930F37FBC89B6CDC697' 
WHERE UniversityID = '2023S-IT-0001';

UPDATE Students SET PasswordHash = 'B6BC7B58510319A151D168BA3D5AECB3AC0A9708D06DD930F37FBC89B6CDC697' WHERE UniversityID = '2024F-BCS-106'

SELECT * FROM Students