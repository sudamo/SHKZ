MERGE INTO  ICInventory AS IC
USING
(
	SELECT 123 FItemID, 123 FStockID,123 FQty
) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID
WHEN MATCHED
    THEN UPDATE SET FQty = IC.FQty + DT.FQty
WHEN NOT MATCHED
    THEN INSERT(FBrNo,FItemID,FStockID,FQty) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty);

select * from ICInventory