SET NOCOUNT ON;
CREATE TABLE #UtiQualification(VenueId uniqueidentifier NOT NULL,AccountId nvarchar(40) NOT NULL,ExecutionId nvarchar(100) NOT NULL,TradeUti nvarchar(100) NOT NULL);
CREATE UNIQUE INDEX IX_Execution ON #UtiQualification(VenueId,AccountId,ExecutionId);
CREATE UNIQUE INDEX IX_Uti ON #UtiQualification(VenueId,AccountId,TradeUti) WHERE TradeUti <> N'';
DECLARE @v uniqueidentifier='11111111-1111-1111-1111-111111111111';
INSERT #UtiQualification VALUES(@v,'demo','exec-1',''),(@v,'demo','exec-2',''),(@v,'demo','exec-3','known-uti');
BEGIN TRY
 INSERT #UtiQualification VALUES(@v,'demo','exec-4','known-uti');
 THROW 51000,'DUPLICATE_NONEMPTY_UTI_ACCEPTED',1;
END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN(2601,2627) THROW; END CATCH;
BEGIN TRY
 INSERT #UtiQualification VALUES(@v,'demo','exec-1','');
 THROW 51000,'DUPLICATE_EXECUTION_ACCEPTED',1;
END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN(2601,2627) THROW; END CATCH;
IF (SELECT COUNT(*) FROM #UtiQualification)<>3 THROW 51000,'WRONG_ROW_COUNT',1;
SELECT 'PASS_TWO_ABSENT_UTIS_AND_DUPLICATE_GUARDS' AS Result;
DROP TABLE #UtiQualification;
