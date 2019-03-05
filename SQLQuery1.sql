

--/////////////////////////////////////////////////////////////

SELECT * FROM t_TableDescription where UPPER(FTableName) LIKE 'POOrderEntry%'	--FDescription like '%员工%'	--SEORDER:230004 SEOrderEntry:230005
SELECT * FROM t_FieldDescription where FTableID = 200005 and fdescription like '%数量%'	--and FFieldName like '%PPB%'

SELECT * FROM ICBillNo where FBillName like '%调拨%'

--1:外购入库
--2：产品入库
--10：其他入库
--21：销售出库
--24：生产领料
--29：其他出库
--41：仓库调拨
--50：BOM
--71：采购订单--？
--81：销售订单
--85：生产任务单
--88：生产投料单
--
--

--/////////////////////////////////////////////////////////////
--外购入库单
--
--
--
SELECT * FROM POOrder

SELECT FBrNo,FTranType,FROB,Fdate,FNote,FDeptID,FSupplyID,FPurposeID,FSelTranType,FPOMode,FPOStyle,FPOOrdBillNo,FCussentAcctID FROM ICStockBill where FTranType  = 1

SELECT A.FInterID FSourceInterId,AE.FEntryID FSourceEntryID,AE.FItemID,MTL.FUnitID,AE.FPrice,AE.FQty,AE.FStockQty,MTL.FBatchManager FROM POOrder A INNER JOIN POOrderEntry AE ON A.FInterID = AE.FInterID INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID WHERE A.FBillNo = 'POORD18080001' AND MTL.FNumber = '06.06.11.0001.0001'


--产品入库
--282|324|315|16418|16418
--[04.01.01.0004|003|1667|20181106|30|WORK18110002]
--
--select * from vwICBill_41

select a.FInterID,FBillNo,FStatus,FCheckerID,FCheckDate,ae.FItemID,mtl.FNumber,ae.FQty,ae.FBatchNo,ae.FDCStockID,stk.FNumber,isnull(inv.FQty,0) StockQTY,inv.FQty
from ICStockBill a
inner join ICStockBillEntry ae on a.FInterID = ae.FInterID
inner join t_ICItem mtl on ae.FItemID = mtl.FItemID
inner join t_Stock stk on ae.FDCStockID = stk.FItemID
left join ICInventory inv on ae.FItemID = inv.FItemID and ae.FDCStockID = inv.FStockID and ae.FBatchNo = inv.FBatchNo
where a.FBillNo = 'WIN000001'

--领料
--282|324|315|16418|WMS
--[01.01.04.0100|003|*|20180101|6.7|WORK18110002|WMS]
--
SELECT A.FBillNo,A.FTranType,A.FStatus,AE.FItemID,MTL.FNumber,AE.FQty,ISNULL(INV.FQty,0) FStockQty,AE.FBatchNo,AE.FSCStockID,AE.FDCSPID,MTL.FBatchManager,ISNULL(MO.FInterID,0) FSourceInterId,ISNULL(BOME.FDetailID,0) FDetailID,A.FROB
            	FROM ICStockBill A
            	INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID
            	INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
            	LEFT JOIN ICInventory INV ON AE.FItemID = INV.FItemID AND AE.FBatchNo = INV.FBatchNo AND AE.FSCStockID = INV.FStockID AND AE.FDCSPID = INV.FStockPlaceID
            	LEFT JOIN ICMO MO ON AE.FICMOInterID = MO.FInterID
            	LEFT JOIN PPBOM BOM ON MO.FInterID = BOM.FICMOInterID
            	LEFT JOIN PPBOMEntry BOME ON BOM.FInterID = BOME.FInterID
            	WHERE A.FBillNo = 'SOUT18111284'

SELECT A.FInterID,BOME.FItemID,MTL.FUnitID,A.FNote,MTL.FItemID FCostOBJID,MTL.FBatchManager,MTL.FNumber
FROM ICMO A
INNER JOIN PPBOM BOM ON A.FInterID = BOM.FICMOInterID
INNER JOIN PPBOMEntry BOME ON BOM.FInterID = BOME.FInterID
INNER JOIN t_ICItem MTL ON BOME.FItemID = MTL.FItemID
WHERE A.FBillNo = 'WORK18110310' AND MTL.FNumber = '01.01.04.0100'--117081

select * from ICMO where fbillno in('WORK18110396','WORK18110036')
SELECT A.FInterID FSourceInterId,BOME.FItemID,MTL.FNumber FItem,MTL.FUnitID,A.FNote,MTL.FItemID FCostOBJID,BOME.FDetailID,BOM.FInterID FPPBomID,BOME.FEntryID FPPBomEntryID,(BOME.FQtyPick - BOME.FStockQty) FStockQty,MTL.FBatchManager FROM ICMO A INNER JOIN PPBOM BOM ON A.FInterID = BOM.FICMOInterID INNER JOIN PPBOMEntry BOME ON BOM.FInterID = BOME.FInterID INNER JOIN t_ICItem MTL ON BOME.FItemID = MTL.FItemID WHERE A.fbillno in('WORK18110396','WORK18110036')

select * from PPBOMEntry where FDetailID in(456518,456519)
select A.FBillNo,MTL.FNumber from PPBOM A INNER JOIN PPBOMEntry AE ON A.FInterID = AE.FInterID INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID  WHERE A.FBillNo = 'PBOM18110003'


--审核
--
SELECT A.FBillNo,A.FTranType,A.FStatus,AE.FItemID,AE.FQty,ISNULL(INV.FQty,0) FStockQty,ISNULL(AE.FDCStockID,0)FDCStockID,ISNULL(AE.FDCSPID,0) FDCSPID,ISNULL(AE.FSCStockID,0) FSCStockID,ISNULL(AE.FSCSPID,0) FSCSPID,AE.FBatchNo,AE.FSourceBillNo,AE.FSourceInterId,AE.FSourceEntryID,MTL.FBatchManager,A.FROB
FROM ICStockBill A
INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID
INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
LEFT JOIN ICInventory INV ON AE.FItemID = INV.FItemID AND AE.FBatchNo = INV.FBatchNo AND AE.FSCStockID = INV.FStockID AND AE.FSCSPID = INV.FStockPlaceID
WHERE A.FBillNo = 'CHG18110166'
GROUP BY A.FBillNo,A.FTranType,A.FStatus,AE.FItemID,AE.FQty,INV.FQty,AE.FDCStockID,AE.FDCSPID,AE.FSCStockID,AE.FSCSPID,AE.FBatchNo,AE.FSourceBillNo,AE.FSourceInterId,AE.FSourceEntryID,MTL.FBatchManager,A.FROB

SELECT MTL.FNumber,SUM(AE.FQty) FQty,ISNULL(INV.FQty,0) FStockQty,CASE WHEN SUM(AE.FQty) > ISNULL(INV.FQty,0) THEN 1 ELSE 0 END ErrFlag
FROM ICStockBill A
INNER JOIN ICstockbillentry AE ON A.FInterID = AE.FInterID
INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
LEFT JOIN ICInventory INV ON AE.FItemID = INV.FItemID AND AE.FSCStockID = INV.FStockID AND AE.FBatchNo = INV.FBatchNo
WHERE A.FInterID = 468360
GROUP BY MTL.FNumber,INV.FQty

--/////////////////////////////////////////////////////////////
SELECT * FROM t_Stock
SELECT * FROM t_StockPlace
select * from t_Supplier where FItemID =2412

select * from IC_MaxNum where FTableName = 'ICStockBill' and FUserID = 16394 order by FNumber
select * from ICMaxNum where FTableName = 'ICStockBill'

select *
from ICInventory
where FItemID = 5174 AND FStockID = 8287 AND FStockPlaceID = 296 AND FBatchNo = '130630'

SELECT top 1000 A.FInterID,A.FDate, A.FBillNo,A.FTranType,A.FStatus,AE.FItemID,AE.FQty,AE.FDCStockID,AE.FSCStockID,AE.FBatchNo,AE.FSourceTranType,AE.FSourceBillNo,AE.FSourceInterId,AE.FSourceEntryID,AE.FICMOBillNo,AE.FICMOInterID,AE.FPPBomEntryID,AE.FSCBillNo,AE.FSCBillInterID,A.FPurposeID,MTL.FBatchManager,AE.FDCSPID,AE.FSCSPID,FCussentAcctID,FRefType,FMarketingStyle
FROM ICStockBill A
INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID
INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
WHERE A.FTranType = 1 --and A.FBillNo like 'SOUT18%'
ORDER BY A.FInterID DESC

select FChkPassItem from ICStockBillEntry where finterid = 13223
select * from DM_ICBillNo

select top 1000 fkfdate from ICStockBillEntry where fsourcetrantype = 85 order by finterid desc
--

SELECT A.FBillNo,A.FTranType,A.FStatus,AE.FItemID,AE.FQty,ISNULL(INV.FQty,0) FStockQty,ISNULL(AE.FDCStockID,0)FDCStockID,ISNULL(AE.FDCSPID,0) FDCSPID,ISNULL(AE.FSCStockID,0) FSCStockID,ISNULL(AE.FSCSPID,0) FSCSPID,AE.FBatchNo,AE.FSourceBillNo,AE.FSourceInterId,AE.FSourceEntryID,MTL.FBatchManager
FROM ICStockBill A
INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID
INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
LEFT JOIN ICInventory INV ON AE.FItemID = INV.FItemID AND AE.FBatchNo = INV.FBatchNo AND (AE.FSCStockID = INV.FStockID AND AE.FSCSPID = INV.FStockPlaceID)
WHERE A.FBillNo = 'CHG18110166'

--Audit领料单
SELECT A.FBillNo,A.FTranType,A.FStatus,BOME.FItemID,MTL.FNumber,BOME.FQty,ISNULL(INV.FQty,0) FStockQty,AE.FBatchNo,AE.FSCStockID,AE.FDCSPID,MTL.FBatchManager,MO.FInterID FSourceInterId,BOME.FDetailID
				FROM ICStockBill A
				INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID
				INNER JOIN ICMO MO ON AE.FICMOInterID = MO.FInterID
				INNER JOIN PPBOM BOM ON MO.FInterID = BOM.FICMOInterID
				INNER JOIN PPBOMEntry BOME ON BOM.FInterID = BOME.FInterID AND BOME.FQty > 0
				INNER JOIN t_ICItem MTL ON BOME.FItemID = MTL.FItemID
				LEFT JOIN ICInventory INV ON BOME.FItemID = INV.FItemID AND AE.FBatchNo = INV.FBatchNo AND AE.FSCStockID = INV.FStockID AND AE.FDCSPID = INV.FStockPlaceID
WHERE A.FBillNo = 'SOUT18111284'

--调拨单
SELECT MTL.FNumber,MTL.FItemID,MTL.FUnitID,MTL.FBatchManager FROM t_ICItem MTL WHERE MTL.FNumber = '01.02.01.0004'

SELECT MTL.FItemID,MTL.FUnitID, CASE WHEN 100 > ISNULL(INV.FQty,0) THEN -1 ELSE ISNULL(INV.FQty,0) END FQty
FROM t_ICItem MTL
LEFT JOIN ICInventory INV ON MTL.FItemID = INV.FItemID AND INV.FBatchNo = '130630' AND INV.FStockID = 8287 AND INV.FStockPlaceID = 296
WHERE MTL.FNumber = '01.02.01.0004'

SELECT MTL.FItemID,MTL.FUnitID,ISNULL(INV.FQty,0) FStockQty,CASE WHEN 100 > ISNULL(INV.FQty,0) THEN -1 ELSE 0 END Flag,FStockID,FStockPlaceID
FROM t_ICItem MTL
LEFT JOIN ICInventory INV ON MTL.FItemID = INV.FItemID AND INV.FBatchNo = '130630' AND INV.FStockID = 8287 AND INV.FStockPlaceID = 296
WHERE MTL.FNumber = '01.02.01.0004'


SELECT FItemID,FNumber FROM t_Stock WHERE FNumber IN('003','005')
SELECT FSPID,FNumber FROM t_StockPlace WHERE FSPID >= 296

select top 100 * from ICStockBill where FTranType = 41 order by FInterID desc

select * from ICStockBillEntry where FInterID in (13362,12956)

select * from ICInventory where FItemID = 5174

SELECT A.FSupplyID,AE.FNote FROM [47.96.13.181].[AIS20130728102030].[dbo].POOrder A INNER JOIN [47.96.13.181].[AIS20130728102030].[dbo].POOrderEntry AE ON A.FInterID = AE.FInterID WHERE A.FBillNo = 'POORD181123416'

SELECT A.FInterID FSourceInterId,AE.FEntryID FSourceEntryID,AE.FItemID,MTL.FUnitID,AE.FPrice,AE.FQty,AE.FStockQty,MTL.FBatchManager FROM [47.96.13.181].[AIS20130728102030].[dbo].POOrder A INNER JOIN [47.96.13.181].[AIS20130728102030].[dbo].POOrderEntry AE ON A.FInterID = AE.FInterID INNER JOIN [47.96.13.181].[AIS20130728102030].[dbo].t_ICItem MTL ON AE.FItemID = MTL.FItemID WHERE A.FBillNo = 'POORD181123416' AND MTL.FNumber = '01.01.01.0004'

select * from DM_ICBillNo

select top 100 * from PoOrder order by FInterID desc --POORD181223763
 
select * from poorderentry where finterid=25950--16320 20000/19315 10000/19127 10000

select * from ICInventory where fitemid = 19127 --16320 181117/181119 532 3341-0	/19315 181015 532 3330-866	/19127 171018/171019 532 3571-18

select * from t_ICItem where fitemid = 19127
select * from t_Stock where FItemID = 532--003

select * from t_StockPlace where fspid = 3571--10278


--272|324|315|16418|POORD181223763|abc
--[01.06.08.0035|003|11293|181117/181119|20000|POORD181223763|1],[01.06.08.0054|003|11282|181015|10000|POORD181223763|2],[01.06.08.0047|003|9191|171018/171019|10000|POORD181223763|3]

SELECT A.FInterID FSourceInterId,AE.FEntryID FSourceEntryID,A.FSupplyID,AE.FItemID,MTL.FUnitID,AE.FPrice,MTL.FBatchManager,O.FStockQty,O.FSEQ,ae.fqty,ae.fstockqty,ae.FAuxStockQty,ae.FSecStockQty,ae.FCommitQty,ae.AuxCommitQty,ae.FSecCommitQty
FROM [192.168.1.7].AIS20190103151139.dbo.POOrder A
INNER JOIN [192.168.1.7].AIS20190103151139.dbo.POOrderEntry AE ON A.FInterID = AE.FInterID
INNER JOIN [192.168.1.7].AIS20190103151139.dbo.t_ICItem MTL ON AE.FItemID = MTL.FItemID
INNER JOIN 
(
SELECT FInterID,FItemID,SUM(FQty - FStockQty) FStockQty,COUNT(*) FSEQ
FROM [192.168.1.7].AIS20190103151139.dbo.POOrderEntry
GROUP BY FInterID,FItemID
)O ON O.FInterID = AE.FInterID AND O.FItemID = AE.FItemID
WHERE A.FBillNo = 'POORD190124053' AND ae.fitemid = 4383 --MTL.FNumber = '01.01.01.0003'


select * 
from [192.168.1.7].AIS20190103151139.dbo.ICStockBill where fbillno = 'WIN1901180001'

select top 1 FStockQty,FAuxStockQty,FSecStockQty,FCommitQty,FAuxCommitQty,FSecCommitQty from POOrderEntry


--20190226
SELECT A.FBillNo 订单编号,MTL.FNumber 物料编码,AE.FPrice 单价,AE.FQty - AE.FStockQty 最大可入库数量,AE.FQty 订货数量,AE.FStockQty 已入库数量,FCommitQty 到货数量,FAuxCommitQty 辅助到货数量
FROM [192.168.1.7].AIS20130728102030.dbo.POOrder A
INNER JOIN [192.168.1.7].AIS20130728102030.dbo.POOrderEntry AE ON A.FInterID = AE.FInterID
INNER JOIN [192.168.1.7].AIS20130728102030.dbo.t_ICItem MTL ON AE.FItemID = MTL.FItemID WHERE A.FBillNo = 'POORD190224431'



SELECT A.FBillNo 订单编号,MTL.FNumber 物料编码,AE.FPrice 单价,AE.FQty - AE.FStockQty 最大可入库数量,AE.FQty 订货数量,AE.FStockQty 已入库数量,FCommitQty 到货数量
FROM POOrder A
INNER JOIN POOrderEntry AE ON A.FInterID = AE.FInterID
INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
WHERE A.FBillNo = 'POORD190224431'


