
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER OFF
GO

/******************************************************************************
 * PROCEDURE NAME: DM_GetICBillNo                                             *
 *     CREATED BY: DM                                                         *
 *  CREATION DATE: 2018-11-01                                                 *
 *    DESCRIPTION: 编码规则 单据类别+年月日(6位)+流水号(4位)                  *
 *     PARAMETERS: @FBillType INT 单据类别；@BillNo VARCHAR OUTPUT 生成单号   *
 ******************************************************************************/

CREATE PROCEDURE [dbo].[DM_GetICBillNo]
	@FBillType	INT,
	@BillNo		VARCHAR(50) OUTPUT
AS

DECLARE
	@FBillID INT,
	@FPreLetter VARCHAR(50) = '',
	@YearMonthDay VARCHAR(6) = SUBSTRING(CONVERT(VARCHAR(8),GETDATE(),112),3,6),
	@FCurNo VARCHAR(6) = '0001',
	@TempYMD VARCHAR(6) = ''

BEGIN TRANSACTION
	IF EXISTS (SELECT 1 FROM DM_ICBillNo WHERE FBillID = @FBillType)
	BEGIN
		SELECT @FPreLetter = FPreLetter,@TempYMD = YearMonthDay,@FCurNo = FCurNo FROM DM_ICBillNo WHERE FBillID = @FBillType
		IF @TempYMD = @YearMonthDay
		BEGIN
			SELECT @FCurNo = SUBSTRING(CONVERT(VARCHAR,CONVERT(INT,@FCurNo) + 1),3,4)
			UPDATE DM_ICBillNo SET FCurNo = '10' + @FCurNo WHERE FBillID = @FBillType
		END
		ELSE
		BEGIN
			SELECT @FCurNo = '0001'
			UPDATE DM_ICBillNo SET YearMonthDay = @YearMonthDay,FCurNo = '100001' WHERE FBillID = @FBillType
		END
	END
	ELSE
	BEGIN
		SELECT @FBillID = FBillID,@FPreLetter = ISNULL(FPreLetter,'') FROM ICBillNo WHERE FBillID = @FBillType

		INSERT INTO DM_ICBillNo(FBillID,FPreLetter,YearMonthDay,FCurNo)
		VALUES(@FBillID,@FPreLetter,@YearMonthDay,'10' + @FCurNo)
	END

	SELECT @BillNo = @FPreLetter + @YearMonthDay + @FCurNo

	IF @@ERROR = 0
		COMMIT
	ELSE
	BEGIN
		ROLLBACK
		SELECT @BillNo = ''
	END
RETURN

GO


