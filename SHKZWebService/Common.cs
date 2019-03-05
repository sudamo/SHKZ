using System;
using System.Data;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;

namespace SHKZWebService
{
    /// <summary>
    /// 方法
    /// </summary>
    static class Common
    {
        #region STATIC
        private static string C_CONNECTIONSTRING;
        static Common()
        {
            C_CONNECTIONSTRING = ConfigurationManager.AppSettings["C_CONNECTIONSTRING"];
        }
        #endregion

        #region 连接测试
        /// <summary>
        /// 连接测试
        /// </summary>
        /// <returns>连接成功/连接失败</returns>
        public static string TestConnection()
        {
            bool bReturn = false;
            SqlConnection conn = new SqlConnection(C_CONNECTIONSTRING);
            try
            {
                conn.Open();
                if (conn.State == ConnectionState.Open)
                    bReturn = true;
            }
            catch
            {
                bReturn = false;
            }
            finally
            {
                conn.Close();
            }
            return bReturn ? "连接成功" : "连接失败";
        }
        #endregion

        #region 审核单据
        /// <summary>
        /// 审核单据 - 目前只支持 ICStockBill 单据
        /// </summary>
        /// <param name="pFBillNo">单号</param>
        /// <param name="pFCheckerID">审核人ID</param>
        /// <returns>审核成功/审核失败：失败信息</returns>
        public static string AuditBill(string pFBillNo, int pFCheckerID)
        {
            string strSQL;
            if (pFBillNo.Contains("CHG"))//调拨单
                strSQL = @"SELECT A.FBillNo,A.FTranType,A.FStatus,AE.FItemID,MTL.FNumber,AE.FQty,ISNULL(INV.FQty,0) FStockQty,ISNULL(AE.FDCStockID,0)FDCStockID,ISNULL(AE.FDCSPID,0) FDCSPID,ISNULL(AE.FSCStockID,0) FSCStockID,ISNULL(AE.FSCSPID,0) FSCSPID,AE.FBatchNo,AE.FSourceBillNo,AE.FSourceInterId,AE.FSourceEntryID,MTL.FBatchManager,A.FROB
                FROM ICStockBill A
                INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID
                INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
                LEFT JOIN ICInventory INV ON AE.FItemID = INV.FItemID AND AE.FBatchNo = INV.FBatchNo AND AE.FSCStockID = INV.FStockID AND AE.FSCSPID = INV.FStockPlaceID
                WHERE A.FBillNo = '" + pFBillNo + "'";
            else if (pFBillNo.Contains("SOUT"))//生产领料单
                strSQL = @"SELECT A.FBillNo,A.FTranType,A.FStatus,AE.FItemID,MTL.FNumber,AE.FQty,ISNULL(INV.FQty,0) FStockQty,AE.FBatchNo,AE.FSCStockID,AE.FDCSPID,MTL.FBatchManager,ISNULL(MO.FInterID,0) FSourceInterId,ISNULL(BOME.FDetailID,0) FDetailID,A.FROB
            	FROM ICStockBill A
            	INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID
            	INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
            	LEFT JOIN ICInventory INV ON AE.FItemID = INV.FItemID AND AE.FBatchNo = INV.FBatchNo AND AE.FSCStockID = INV.FStockID AND AE.FDCSPID = INV.FStockPlaceID
            	LEFT JOIN ICMO MO ON AE.FICMOInterID = MO.FInterID
            	LEFT JOIN PPBOM BOM ON MO.FInterID = BOM.FICMOInterID
            	LEFT JOIN PPBOMEntry BOME ON BOM.FInterID = BOME.FInterID
            	WHERE A.FBillNo = '" + pFBillNo + "'";
            else//其他单据
                strSQL = @"SELECT A.FBillNo,A.FTranType,A.FStatus,AE.FItemID,MTL.FNumber,AE.FQty,ISNULL(INV.FQty,0) FStockQty,ISNULL(AE.FDCStockID,0)FDCStockID,ISNULL(AE.FDCSPID,0) FDCSPID,ISNULL(AE.FSCStockID,0) FSCStockID,ISNULL(AE.FSCSPID,0) FSCSPID,AE.FBatchNo,AE.FSourceBillNo,AE.FSourceInterId,AE.FSourceEntryID,MTL.FBatchManager,A.FROB
                FROM ICStockBill A
                INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID
                INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
                LEFT JOIN ICInventory INV ON AE.FItemID = INV.FItemID AND AE.FBatchNo = INV.FBatchNo AND AE.FDCStockID = INV.FStockID AND AE.FDCSPID = INV.FStockPlaceID
                WHERE A.FBillNo = '" + pFBillNo + "'";

            object obj = SqlOperation(3, strSQL);

            if (obj == null || ((DataTable)obj).Rows.Count == 0)
                return "审核失败：单据不存在。";

            DataTable dt = (DataTable)obj;

            if (dt.Rows[0]["FStatus"].ToString() == "1")
                return "审核失败：单据已经审核。";

            List<int> lstTranType = new List<int>();
            //lstTranType.Add(1);//外购入库 WIN
            //lstTranType.Add(2);//产品入库 CIN
            lstTranType.Add(10);//其他入库 QIN
            lstTranType.Add(21);//销售出库 XOUT
            lstTranType.Add(24);//生产领料 SOUT
            lstTranType.Add(29);//其他出库 QOUT
            lstTranType.Add(41);//仓库调拨 CHG

            if (!lstTranType.Contains(int.Parse(dt.Rows[0]["FTranType"].ToString())))
            {
                return "审核失败：仅支持其他入库、销售出库、生产领料、其他出库和调拨单。";
            }

            #region 库存判断
            if (int.Parse(dt.Rows[0]["FTranType"].ToString()) != 10 && int.Parse(dt.Rows[0]["FROB"].ToString()) != -1)//非其他入库且非红字单据需要比较即使库存数量
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    if (decimal.Parse(dt.Rows[i]["FQty"].ToString()) > decimal.Parse(dt.Rows[i]["FStockQty"].ToString()))
                        return "审核失败：物料[" + dt.Rows[i]["FNumber"].ToString() + "]需求数量[" + dt.Rows[i]["FQty"].ToString() + "]大于库存数量[" + dt.Rows[i]["FStockQty"].ToString() + "]";
                }
            }
            #endregion

            #region 反写库存
            try
            {
                switch (int.Parse(dt.Rows[0]["FTranType"].ToString()))
                {
                    case 10://其他入库
                        for (int i = 0; i < dt.Rows.Count; i++)
                        {
                            if (dt.Rows[i]["FDCSPID"] == null || dt.Rows[i]["FDCSPID"].ToString() == "0")
                                strSQL = @"MERGE INTO ICInventory AS IC
                                USING
                                (
                                    SELECT " + dt.Rows[i]["FItemID"].ToString() + " FItemID," + dt.Rows[i]["FQty"].ToString() + " FQty,'" + dt.Rows[i]["FBatchNo"].ToString() + "' FBatchNo, " + dt.Rows[i]["FDCStockID"].ToString() + " FStockID, " + dt.Rows[i]["FDCSPID"].ToString() + @" FSPID
                                ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                                WHEN MATCHED
                                    THEN UPDATE SET FQty = IC.FQty + DT.FQty
                                WHEN NOT MATCHED
                                    THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);";
                            else
                                strSQL = @"MERGE INTO ICInventory AS IC
                                USING
                                (
                                    SELECT " + dt.Rows[i]["FItemID"].ToString() + " FItemID," + dt.Rows[i]["FQty"].ToString() + " FQty,'" + dt.Rows[i]["FBatchNo"].ToString() + "' FBatchNo, " + dt.Rows[i]["FDCStockID"].ToString() + " FStockID, " + dt.Rows[i]["FDCSPID"].ToString() + @" FSPID
                                ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                                WHEN MATCHED
                                    THEN UPDATE SET FQty = IC.FQty + DT.FQty
                                WHEN NOT MATCHED
                                    THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);";

                            SqlOperation(0, strSQL);
                        }
                        break;
                    case 21://销售出库
                    case 29://其他出库
                        for (int i = 0; i < dt.Rows.Count; i++)
                        {
                            if (dt.Rows[i]["FDCSPID"] == null || dt.Rows[i]["FDCSPID"].ToString() == "0")
                                strSQL = "UPDATE ICInventory SET FQty = FQty - " + dt.Rows[i]["FQty"].ToString() + " WHERE FItemID = " + dt.Rows[i]["FItemID"].ToString() + " AND FBatchNo = '" + dt.Rows[i]["FBatchNo"].ToString() + "' AND FStockID = " + dt.Rows[i]["FDCStockID"].ToString();
                            else
                                strSQL = "UPDATE ICInventory SET FQty = FQty - " + dt.Rows[i]["FQty"].ToString() + " WHERE FItemID = " + dt.Rows[i]["FItemID"].ToString() + " AND FBatchNo = '" + dt.Rows[i]["FBatchNo"].ToString() + "' AND FStockID = " + dt.Rows[i]["FDCStockID"].ToString() + " AND FStockPlaceID = " + dt.Rows[i]["FDCSPID"].ToString();

                            SqlOperation(0, strSQL);
                        }
                        break;
                    case 24://生产领料
                        for (int i = 0; i < dt.Rows.Count; i++)
                        {
                            if (int.Parse(dt.Rows[0]["FROB"].ToString()) == 1)//1：蓝字
                            {
                                if (int.Parse(dt.Rows[i]["FSourceInterId"].ToString()) != 0 && int.Parse(dt.Rows[i]["FDetailID"].ToString()) != 0)
                                    strSQL = @"MERGE INTO ICInventory AS IC
							        USING
							        (
							         SELECT " + dt.Rows[i]["FItemID"].ToString() + " FItemID, " + dt.Rows[i]["FSCStockID"].ToString() + " FStockID," + dt.Rows[i]["FDCSPID"].ToString() + " FSPID," + Math.Abs(decimal.Parse(dt.Rows[i]["FQty"].ToString())) + " FQty,'" + dt.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
							        ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
							        WHEN MATCHED
								        THEN UPDATE SET FQty = IC.FQty - DT.FQty
							        WHEN NOT MATCHED
								        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo); 
							        UPDATE ICMO SET FStockQty = FStockQty + " + dt.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dt.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dt.Rows[i]["FSourceInterId"].ToString() + @"; 
							        UPDATE PPBOMEntry SET FStockQty = FStockQty + " + dt.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dt.Rows[i]["FQty"].ToString() + " WHERE FDetailID = " + dt.Rows[i]["FDetailID"].ToString() + ";";
                                else
                                    strSQL = @"MERGE INTO ICInventory AS IC
							        USING
							        (
							         SELECT " + dt.Rows[i]["FItemID"].ToString() + " FItemID, " + dt.Rows[i]["FSCStockID"].ToString() + " FStockID," + dt.Rows[i]["FDCSPID"].ToString() + " FSPID," + Math.Abs(decimal.Parse(dt.Rows[i]["FQty"].ToString())) + " FQty,'" + dt.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
							        ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
							        WHEN MATCHED
								        THEN UPDATE SET FQty = IC.FQty - DT.FQty
							        WHEN NOT MATCHED
								        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);";
                            }
                            else//-1：红字，红字的数量未负数，取绝对值添加到库存
                            {
                                if (int.Parse(dt.Rows[i]["FSourceInterId"].ToString()) != 0 && int.Parse(dt.Rows[i]["FDetailID"].ToString()) != 0)
                                    strSQL = @"MERGE INTO ICInventory AS IC
							        USING
							        (
							         SELECT " + dt.Rows[i]["FItemID"].ToString() + " FItemID, " + dt.Rows[i]["FSCStockID"].ToString() + " FStockID," + dt.Rows[i]["FDCSPID"].ToString() + " FSPID," + Math.Abs(decimal.Parse(dt.Rows[i]["FQty"].ToString())) + " FQty,'" + dt.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
							        ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
							        WHEN MATCHED
								        THEN UPDATE SET FQty = IC.FQty + DT.FQty
							        WHEN NOT MATCHED
								        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo); 
							        UPDATE ICMO SET FStockQty = FStockQty + " + dt.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dt.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dt.Rows[i]["FSourceInterId"].ToString() + @"; 
							        UPDATE PPBOMEntry SET FStockQty = FStockQty + " + dt.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dt.Rows[i]["FQty"].ToString() + " WHERE FDetailID = " + dt.Rows[i]["FDetailID"].ToString() + ";";
                                else
                                    strSQL = @"MERGE INTO ICInventory AS IC
							        USING
							        (
							         SELECT " + dt.Rows[i]["FItemID"].ToString() + " FItemID, " + dt.Rows[i]["FSCStockID"].ToString() + " FStockID," + dt.Rows[i]["FDCSPID"].ToString() + " FSPID," + Math.Abs(decimal.Parse(dt.Rows[i]["FQty"].ToString())) + " FQty,'" + dt.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
							        ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
							        WHEN MATCHED
								        THEN UPDATE SET FQty = IC.FQty + DT.FQty
							        WHEN NOT MATCHED
								        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);";
                            }

                            SqlOperation(0, strSQL);
                        }
                        break;
                    case 41://仓库调拨
                        for (int i = 0; i < dt.Rows.Count; i++)
                        {
                            if (dt.Rows[i]["FDCStockID"] == dt.Rows[i]["FSCStockID"] && dt.Rows[i]["FDCSPID"] == dt.Rows[i]["FSCSPID"])
                                continue;

                            strSQL = @"
                            --源仓扣库存
                            MERGE INTO ICInventory AS IC
                            USING
                            (
                                SELECT " + dt.Rows[i]["FItemID"].ToString() + " FItemID," + dt.Rows[i]["FQty"].ToString() + " FQty,'" + dt.Rows[i]["FBatchNo"].ToString() + "' FBatchNo, " + dt.Rows[i]["FSCStockID"].ToString() + @" FStockID, " + dt.Rows[i]["FSCSPID"].ToString() + @" FSPID
                            ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                            WHEN MATCHED
                                THEN UPDATE SET FQty = IC.FQty - DT.FQty
                            WHEN NOT MATCHED
                                THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,-DT.FQty,DT.FBatchNo);
                            --目标仓加库存
                            MERGE INTO ICInventory AS IC
                            USING
                            (
                                SELECT " + dt.Rows[i]["FItemID"].ToString() + " FItemID," + dt.Rows[i]["FQty"].ToString() + " FQty,'" + dt.Rows[i]["FBatchNo"].ToString() + "' FBatchNo, " + dt.Rows[i]["FDCStockID"].ToString() + " FStockID, " + dt.Rows[i]["FDCSPID"].ToString() + @" FSPID
                            ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                            WHEN MATCHED
                                THEN UPDATE SET FQty = IC.FQty + DT.FQty
                            WHEN NOT MATCHED
                                THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);";

                            SqlOperation(0, strSQL);
                        }
                        break;
                        //default:
                        //    {
                        //        strSQL = "SELECT '未知单据'";
                        //    }
                        //    break;
                }
            }
            catch (Exception ex)
            {
                return "审核失败：" + ex.Message;
            }
            #endregion

            //Execute Audit Bill
            strSQL = "UPDATE ICStockBill SET FCheckDate = GETDATE(),FStatus = 1,FNote = 'WMS_Audit',FCheckerID = " + pFCheckerID.ToString() + " WHERE FBillNo = '" + pFBillNo + "'";
            SqlOperation(0, strSQL);

            return "审核成功";
        }
        #endregion

        #region 生产入库单
        /// <summary>
        /// 生产入库单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FCheckerID</param>
        /// <param name="pDetails">表体：[FStockNumber|FBatchNo|FQty|FSourceBillNo]</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        public static string ICStockBillForInStock(string pHead, string pDetails)
        {
            object obj;
            string strSQL;
            DataTable dtDtl;
            DataRow dr;
            SqlConnection conn;

            int FInterID;
            string FBillNo;

            GetICMaxIDAndBillNo(2, out FInterID, out FBillNo);

            if (FBillNo.IndexOf("Error") >= 0)
                return "no@x001:" + FBillNo;

            //ICMO
            int MOFInterID;

            //定义表头字段
            int FDeptID, FSManagerID, FFManagerID, FBillerID, FCheckerID;

            //定义表体字段
            int FItemID, FUnitID, FDCStockID, FDCSPID;
            decimal FQty;
            string FItem, FDCStock, FDCSP, FBatchNo = string.Empty, FSourceBillNo, FNote;

            //物料
            bool FBatchManager = false;

            #region 字段赋值
            try
            {
                FDeptID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FDeptID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FSManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FFManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FFManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FBillerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FBillerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FCheckerID = int.Parse(pHead.Substring(pHead.IndexOf("|") + 1));//FCheckerID

                //
                dtDtl = new DataTable();
                dtDtl.Columns.Add("FItemID");
                dtDtl.Columns.Add("FUnitID");
                dtDtl.Columns.Add("FDCStockID");
                dtDtl.Columns.Add("FDCSPID");
                dtDtl.Columns.Add("FBatchNo");

                dtDtl.Columns.Add("FQty");
                dtDtl.Columns.Add("FNote");
                dtDtl.Columns.Add("FSourceBillNo");
                dtDtl.Columns.Add("FSourceInterId");

                string strTemp;
                do
                {
                    if (pDetails.IndexOf("],[") > 0)
                    {
                        strTemp = pDetails.Substring(0, pDetails.IndexOf("]") + 1);
                        pDetails = pDetails.Substring(pDetails.IndexOf("]") + 2);
                    }
                    else
                    {
                        strTemp = pDetails;
                        pDetails = "";
                    }

                    FDCStock = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//StockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY

                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSourceBillNo = strTemp.Substring(0, strTemp.IndexOf("]"));//ICMOBillNo

                    obj = SqlOperation(3, "SELECT A.FInterID FSourceInterId,A.FItemID,MTL.FNumber FItem,MTL.FUnitID,A.FNote,MTL.FBatchManager FROM ICMO A INNER JOIN t_ICItem MTL ON A.FItemID = MTL.FItemID WHERE A.FBillNo = '" + FSourceBillNo + "'");
                    if (obj == null || ((DataTable)obj).Rows.Count == 0)
                        return "no@未找到单据信息[" + FSourceBillNo + "]";

                    MOFInterID = int.Parse(((DataTable)obj).Rows[0]["FSourceInterId"].ToString());//FInterID
                    FItemID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//MTLID
                    FItem = ((DataTable)obj).Rows[0]["FItem"].ToString();//MTLNumber
                    FUnitID = int.Parse(((DataTable)obj).Rows[0]["FUnitID"].ToString());//UnitID
                    FNote = ((DataTable)obj).Rows[0]["FNote"].ToString();//FNote

                    FBatchManager = ((DataTable)obj).Rows[0]["FBatchManager"].ToString() == "0" ? false : true;//是否采用业务批次管理

                    if (FDCStock == "")
                        FDCStockID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓库信息[" + FDCStock + "]";

                        FDCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FDCStockID
                    }

                    if (FDCSP == "")
                        FDCSPID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FSPID FROM t_StockPlace WHERE FNumber = '" + FDCSP + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓位信息[" + FDCSP + "]";
                        FDCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FSCSPID
                    }

                    if (FBatchNo.Equals(string.Empty) && FBatchManager)
                    {
                        return "no@[" + FItem + "]物料已经启用批次号管理，请携带批次号。";
                    }

                    dr = dtDtl.NewRow();

                    dr["FItemID"] = FItemID;
                    dr["FUnitID"] = FUnitID;
                    dr["FDCStockID"] = FDCStockID;
                    dr["FDCSPID"] = FDCSPID;
                    dr["FBatchNo"] = FBatchNo;

                    dr["FQty"] = FQty;
                    dr["FNote"] = FNote;
                    dr["FSourceBillNo"] = FSourceBillNo;
                    dr["FSourceInterId"] = MOFInterID;

                    dtDtl.Rows.Add(dr);
                }
                while (pDetails.Length > 0);
            }
            catch (Exception ex)
            {
                return "no@x002:" + ex.Message;
            }
            #endregion

            conn = new SqlConnection(C_CONNECTIONSTRING);

            #region 插入表头
            try
            {
                conn.Open();
                SqlCommand cmdH = conn.CreateCommand();
                cmdH.CommandType = CommandType.Text;

                cmdH.Parameters.Add("@FInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillNo", SqlDbType.NVarChar, 255);
                cmdH.Parameters.Add("@FDeptID", SqlDbType.Int);
                cmdH.Parameters.Add("@FSManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FFManagerID", SqlDbType.Int);

                cmdH.Parameters.Add("@FBillerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FCheckerID", SqlDbType.Int);

                cmdH.Parameters["@FInterID"].Value = FInterID;
                cmdH.Parameters["@FBillNo"].Value = FBillNo;
                cmdH.Parameters["@FDeptID"].Value = FDeptID;
                cmdH.Parameters["@FSManagerID"].Value = FSManagerID;
                cmdH.Parameters["@FFManagerID"].Value = FFManagerID;

                cmdH.Parameters["@FBillerID"].Value = FBillerID;
                cmdH.Parameters["@FCheckerID"].Value = FCheckerID;

                strSQL = @"INSERT INTO dbo.ICStockBill(FInterID,FBillNo,FBrNo,FTranType,FROB,FNote,FDate,FDeptID,FPurposeID,FSManagerID,FFManagerID,  FBillerID,FCheckerID,FCheckDate,FStatus,FSelTranType,FPOMode,FPOStyle,FCussentAcctID,FRefType,FMarketingStyle) 
                VALUES (@FInterID,@FBillNo,'0',2,1,'WMS',CONVERT(VARCHAR(10),GETDATE(),120),@FDeptID,0,@FSManagerID,@FFManagerID,   @FBillerID,@FCheckerID,GETDATE(),1,85,36680,252,1020,0,12530)";

                cmdH.CommandText = strSQL;
                cmdH.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                return "no@x003:" + ex.Message;
            }
            finally
            {
                conn.Close();
            }
            #endregion

            #region 插入表体
            conn.Open();
            SqlCommand cmdD = conn.CreateCommand();
            cmdD.CommandType = CommandType.Text;

            cmdD.Parameters.Add("@FInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@FEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@FItemID", SqlDbType.Int);
            cmdD.Parameters.Add("@FBatchNo", SqlDbType.VarChar);
            cmdD.Parameters.Add("@FQty", SqlDbType.Decimal);

            cmdD.Parameters.Add("@FNote", SqlDbType.VarChar);
            cmdD.Parameters.Add("@FUnitID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCSPID", SqlDbType.Int);

            cmdD.Parameters.Add("@FSourceBillNo", SqlDbType.VarChar, 50);
            cmdD.Parameters.Add("@FSourceInterId", SqlDbType.Int);

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                try
                {
                    cmdD.Parameters["@FInterID"].Value = FInterID;
                    cmdD.Parameters["@FEntryID"].Value = i + 1;
                    cmdD.Parameters["@FItemID"].Value = dtDtl.Rows[i]["FItemID"].ToString();
                    cmdD.Parameters["@FBatchNo"].Value = dtDtl.Rows[i]["FBatchNo"].ToString();
                    cmdD.Parameters["@FQty"].Value = dtDtl.Rows[i]["FQty"].ToString();

                    cmdD.Parameters["@FNote"].Value = dtDtl.Rows[i]["FNote"].ToString();
                    cmdD.Parameters["@FUnitID"].Value = dtDtl.Rows[i]["FUnitID"].ToString();
                    cmdD.Parameters["@FDCStockID"].Value = dtDtl.Rows[i]["FDCStockID"].ToString();
                    cmdD.Parameters["@FDCSPID"].Value = dtDtl.Rows[i]["FDCSPID"].ToString();

                    cmdD.Parameters["@FSourceBillNo"].Value = dtDtl.Rows[i]["FSourceBillNo"].ToString();
                    cmdD.Parameters["@FSourceInterId"].Value = dtDtl.Rows[i]["FSourceInterId"].ToString();

                    strSQL = @"INSERT INTO dbo.ICstockbillentry(FInterID,FEntryID,FBrNo,FItemID,FBatchNo,FQty,FQtyMust,FUnitID,FAuxQtyMust,FAuxQty, FDCStockID,FDCSPID,FSourceTranType,FSourceBillNo,FSourceInterId,FChkPassItem,FNote,FPlanMode,FICMOBillNo,FICMOInterID)
                    VALUES(@FInterID,@FEntryID,'0',@FItemID,@FBatchNo,@FQty,@FQty,@FUnitID,@FQty,@FQty, @FDCStockID,@FDCSPID,85,@FSourceBillNo,@FSourceInterId,1058,@FNote,14036,@FSourceBillNo,@FSourceInterId)";

                    cmdD.CommandText = strSQL;
                    cmdD.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    //删除已生成的单据数据
                    SqlOperation(0, "DELETE FROM ICStockBill WHERE FInterID = " + FInterID.ToString() + " DELETE FROM ICstockbillentry WHERE FInterID = " + FInterID.ToString());

                    conn.Close();
                    return "no@x004:" + ex.Message;
                }
            }
            #endregion

            #region 反写库存和生产订单
            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                if (dtDtl.Rows[i]["FDCSPID"] == null || dtDtl.Rows[i]["FDCSPID"].ToString() == "0")
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
                        SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FDCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + "' FBatchNo, " + dtDtl.Rows[i]["FDCSPID"].ToString() + @" FSPID
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty + DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);
                    UPDATE ICMO SET FStockQty = FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FSourceInterId"].ToString() + ";";
                else
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
                        SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FDCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + "' FBatchNo, " + dtDtl.Rows[i]["FDCSPID"].ToString() + @" FSPID
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty + DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);
                    UPDATE ICMO SET FStockQty = FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FSourceInterId"].ToString() + ";";

                SqlOperation(0, strSQL);
            }
            #endregion

            if (conn.State == ConnectionState.Open)
                conn.Close();

            return "yes@ID:" + FInterID.ToString() + ";Number:" + FBillNo;
        }
        #endregion

        #region 外购入库单
        /// <summary>
        /// 外购入库单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FPOOrderBillNo|FNote</param>
        /// <param name="pDetails">表体：[FItemNumber|FDCStockNumber|FDCSPNumber|FBatchNo|FQty|FSourceBillNo|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        public static string ICStockBillForPO(string pHead, string pDetails)
        {
            object obj;
            string strSQL;
            DataTable dtDtl;
            DataRow dr;
            SqlConnection conn;

            int FInterID;
            string FBillNo;

            GetICMaxIDAndBillNo(1, out FInterID, out FBillNo);

            if (FBillNo.IndexOf("Error") >= 0)
                return "no@x001:" + FBillNo;

            //POOrder
            int POFInterID, POFEntryID;
            DataTable dtCheck;

            //定义表头字段
            string FNote, FPOOrderBillNo;
            int FDeptID, FSupplyID, FSManagerID, FFManagerID, FBillerID, POFInterIDH;

            //定义表体字段
            int FItemID, FUnitID, FDCStockID, FDCSPID;
            decimal FPrice, FQty, FStockQty;
            string FNoteD, FItem, FDCStock, FDCSP, FBatchNo, FSourceBillNo;

            //物料
            bool FBatchManager = false;

            #region 字段赋值
            try
            {
                FDeptID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FDeptID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FSManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FFManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FFManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FBillerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FBillerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FPOOrderBillNo = pHead.Substring(0, pHead.IndexOf("|"));//FPOOrderBillNo
                //
                FNote = pHead.Substring(pHead.IndexOf("|") + 1);//FNote

                obj = SqlOperation(3, "SELECT FInterID,FSupplyID FROM POOrder WHERE FBillNo = '" + FPOOrderBillNo + "'");
                if (obj == null || ((DataTable)obj).Rows.Count == 0)
                    return "no@没有此采购订单数据[" + FPOOrderBillNo + "]";

                POFInterIDH = int.Parse(((DataTable)obj).Rows[0]["FInterID"].ToString());
                FSupplyID = int.Parse(((DataTable)obj).Rows[0]["FSupplyID"].ToString());

                //
                dtDtl = new DataTable();
                dtDtl.Columns.Add("FItem");
                dtDtl.Columns.Add("FItemID");//物料ID
                dtDtl.Columns.Add("FUnitID");//单位ID
                dtDtl.Columns.Add("FDCStockID");//仓库
                dtDtl.Columns.Add("FDCSPID");//仓位

                dtDtl.Columns.Add("FBatchNo");//批次号
                dtDtl.Columns.Add("FQty");//未入库数量
                dtDtl.Columns.Add("Fprice");//单价
                dtDtl.Columns.Add("FAmount");//总金额
                dtDtl.Columns.Add("FSourceBillNo");//采购订单号

                dtDtl.Columns.Add("FSourceInterId");//采购订单内码
                dtDtl.Columns.Add("FSourceEntryID");//采购订单分录内码
                dtDtl.Columns.Add("FNote");//备注
                dtDtl.Columns.Add("FStockQty");//入库数量

                string strTemp;
                do
                {
                    if (pDetails.IndexOf("],[") > 0)
                    {
                        strTemp = pDetails.Substring(0, pDetails.IndexOf("]") + 1);
                        pDetails = pDetails.Substring(pDetails.IndexOf("]") + 2);
                    }
                    else
                    {
                        strTemp = pDetails;
                        pDetails = "";
                    }

                    FItem = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//MTLNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//FStockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FStockQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FStockQty

                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSourceBillNo = strTemp.Substring(0, strTemp.IndexOf("|"));//POOrderBillNo
                    //
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FNoteD = strTemp.Substring(0, strTemp.IndexOf("]"));//FNoteD

                    //strSQL = "SELECT A.FInterID FSourceInterId, A.FSupplyID,AE.FEntryID FSourceEntryID, AE.FItemID,MTL.FUnitID,AE.FPrice,AE.FQty - AE.FStockQty FQty,MTL.FBatchManager FROM POOrder A INNER JOIN POOrderEntry AE ON A.FInterID = AE.FInterID INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + "'";

                    strSQL = @"SELECT A.FInterID FSourceInterId,AE.FEntryID FSourceEntryID,A.FSupplyID,AE.FItemID,MTL.FUnitID,AE.FPrice,MTL.FBatchManager,O.FQty
                    FROM POOrder A
                    INNER JOIN POOrderEntry AE ON A.FInterID = AE.FInterID
                    INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
                    INNER JOIN 
                    (
                    SELECT FInterID,FItemID,SUM(FQty - FStockQty) FQty
                    FROM POOrderEntry
                    GROUP BY FInterID,FItemID
                    )O ON O.FInterID = AE.FInterID AND O.FItemID = AE.FItemID
                    WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + "'";


                    obj = SqlOperation(3, strSQL);
                    if (obj == null || ((DataTable)obj).Rows.Count == 0)
                        return "no@未找到物料信息[" + FSourceBillNo + "].[" + FItem + "]";

                    POFInterID = int.Parse(((DataTable)obj).Rows[0]["FSourceInterId"].ToString());//FInterID
                    POFEntryID = int.Parse(((DataTable)obj).Rows[0]["FSourceEntryID"].ToString());//FEntryID
                    FItemID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//MTLID
                    FUnitID = int.Parse(((DataTable)obj).Rows[0]["FUnitID"].ToString());//UnitID
                    FPrice = decimal.Parse(((DataTable)obj).Rows[0]["FPrice"].ToString());//Price
                    FQty = decimal.Parse(((DataTable)obj).Rows[0]["FQty"].ToString());//FQty

                    FBatchManager = ((DataTable)obj).Rows[0]["FBatchManager"].ToString() == "0" ? false : true;//是否采用业务批次管理

                    if (FSupplyID != int.Parse(((DataTable)obj).Rows[0]["FSupplyID"].ToString()))
                    {
                        return "no@表头[" + FPOOrderBillNo + "]的供应商与表体[" + FSourceBillNo + "]的供应商不一致。";
                    }

                    if (FStockQty > FQty)
                    {
                        return "no@[" + FSourceBillNo + "].[" + FItem + "]物料本次入库数量[" + FStockQty.ToString() + "]大于采购订单的未入库数量[" + FQty.ToString() + "]。";
                    }

                    if (FDCStock == "")
                        FDCStockID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓库信息[" + FDCStock + "]";
                        FDCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FStockID
                    }

                    if (FDCSP == "")
                        FDCSPID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FSPID FROM t_StockPlace WHERE FNumber = '" + FDCSP + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓位信息[" + FDCSP + "]";
                        FDCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FDCSPID
                    }

                    if (FBatchNo.Equals(string.Empty) && FBatchManager)
                    {
                        return "no@[" + FItem + "]物料已经启用批次号管理，请携带批次号。";
                    }

                    dr = dtDtl.NewRow();
                    dr["FItem"] = FItem;//物料编码

                    dr["FItemID"] = FItemID;//物料ID
                    dr["FUnitID"] = FUnitID;//单位ID
                    dr["FDCStockID"] = FDCStockID;//仓库ID
                    dr["FDCSPID"] = FDCSPID;//仓位ID
                    dr["FBatchNo"] = FBatchNo;//批次号

                    dr["FQty"] = FQty;//采购数量
                    dr["Fprice"] = FPrice;//单价
                    dr["FAmount"] = FStockQty * FPrice;//金额
                    dr["FSourceBillNo"] = FSourceBillNo;//采购订单
                    dr["FSourceInterId"] = POFInterID;//采购订单内码

                    dr["FSourceEntryID"] = POFEntryID;//采购订单明显内码
                    dr["FNote"] = FNoteD;//备注
                    dr["FStockQty"] = FStockQty;//入库数量

                    dtDtl.Rows.Add(dr);
                }
                while (pDetails.Length > 0);
            }
            catch (Exception ex)
            {
                return "no@x002:" + ex.Message;
            }
            #endregion

            #region 对入库数量的判断
            dtCheck = dtDtl.Clone();//获取汇总FQty的物料信息
            dtCheck.ImportRow(dtDtl.Rows[0]);//把第一条数据写入dtPO中
            //汇总入库情况
            for (int i = 1; i < dtDtl.Rows.Count; i++)
            {
                for (int j = 0; j < dtCheck.Rows.Count; j++)//根据采购单号与物料编码进行匹配
                {
                    if (dtCheck.Rows[j]["FSourceBillNo"].ToString() == dtDtl.Rows[i]["FSourceBillNo"].ToString() && dtCheck.Rows[j]["FItem"].ToString() == dtDtl.Rows[i]["FItem"].ToString())
                    {
                        dtCheck.Rows[j]["FStockQty"] = decimal.Parse(dtCheck.Rows[j]["FStockQty"].ToString()) + decimal.Parse(dtDtl.Rows[i]["FStockQty"].ToString());
                        break;
                    }

                    if (j == dtCheck.Rows.Count - 1)//未匹配到数据
                    {
                        dtCheck.ImportRow(dtDtl.Rows[i]);
                        break;
                    }
                }
            }

            for (int i = 0; i < dtCheck.Rows.Count; i++)
            {
                if (decimal.Parse(dtCheck.Rows[i]["FStockQty"].ToString()) > decimal.Parse(dtCheck.Rows[i]["FQty"].ToString()))//入库数量大于采购数量
                {
                    return "no@[" + dtCheck.Rows[i]["FSourceBillNo"].ToString() + "].[" + dtCheck.Rows[i]["FItem"].ToString() + "]物料本次总入库数量[" + dtCheck.Rows[i]["FStockQty"].ToString() + "]大于未入库数量[" + dtCheck.Rows[i]["FQty"].ToString() + "]。";
                }
            }
            #endregion

            conn = new SqlConnection(C_CONNECTIONSTRING);

            #region 插入主表
            try
            {
                conn.Open();
                SqlCommand cmdH = conn.CreateCommand();
                cmdH.CommandType = CommandType.Text;

                cmdH.Parameters.Add("@FInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillNo", SqlDbType.NVarChar, 255);
                cmdH.Parameters.Add("@FNote", SqlDbType.NVarChar, 255);
                cmdH.Parameters.Add("@FDeptID", SqlDbType.Int);
                cmdH.Parameters.Add("@FSupplyID", SqlDbType.Int);

                cmdH.Parameters.Add("@FSManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FFManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FOrgBillInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FPOOrderBillNo", SqlDbType.NVarChar);

                cmdH.Parameters["@FInterID"].Value = FInterID;
                cmdH.Parameters["@FBillNo"].Value = FBillNo;
                cmdH.Parameters["@FNote"].Value = FNote;
                cmdH.Parameters["@FDeptID"].Value = FDeptID;
                cmdH.Parameters["@FSupplyID"].Value = FSupplyID;

                cmdH.Parameters["@FSManagerID"].Value = FSManagerID;
                cmdH.Parameters["@FFManagerID"].Value = FFManagerID;
                cmdH.Parameters["@FBillerID"].Value = FBillerID;
                cmdH.Parameters["@FOrgBillInterID"].Value = POFInterIDH;
                cmdH.Parameters["@FPOOrderBillNo"].Value = FPOOrderBillNo;

                strSQL = @"INSERT INTO dbo.ICStockBill(FInterID,FBillNo,FBrNo,FTranType,FROB,FNote,FDate,FDeptID,FSupplyID,FPurposeID,   FSManagerID,FFManagerID,FBillerID,FCheckerID,FCheckDate,FStatus,FSelTranType,FPOMode,FPOStyle,FOrgBillInterID,FPOOrdBillNo) 
                VALUES (@FInterID,@FBillNo,'0',1,1,@FNote,CONVERT(VARCHAR(10),GETDATE(),120),@FDeptID,@FSupplyID,0,  @FSManagerID,@FFManagerID,@FBillerID,@FBillerID,GETDATE(),1,71,36680,252,@FOrgBillInterID,@FPOOrderBillNo)";

                cmdH.CommandText = strSQL;
                cmdH.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                return "no@x003:" + ex.Message;
            }
            finally
            {
                conn.Close();
            }
            #endregion

            #region 插入表体
            conn.Open();
            SqlCommand cmdD = conn.CreateCommand();
            cmdD.CommandType = CommandType.Text;

            cmdD.Parameters.Add("@FInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@FEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@FItemID", SqlDbType.Int);
            cmdD.Parameters.Add("@FBatchNo", SqlDbType.VarChar);
            cmdD.Parameters.Add("@FQty", SqlDbType.Decimal);

            cmdD.Parameters.Add("@FUnitID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCSPID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSourceBillNo", SqlDbType.VarChar, 50);
            cmdD.Parameters.Add("@FSourceInterId", SqlDbType.Int);

            cmdD.Parameters.Add("@FSourceEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@Fprice", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FAmount", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FNote", SqlDbType.VarChar);

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                try
                {
                    cmdD.Parameters["@FInterID"].Value = FInterID;
                    cmdD.Parameters["@FEntryID"].Value = i + 1;
                    cmdD.Parameters["@FItemID"].Value = dtDtl.Rows[i]["FItemID"].ToString();
                    cmdD.Parameters["@FBatchNo"].Value = dtDtl.Rows[i]["FBatchNo"].ToString();
                    cmdD.Parameters["@FQty"].Value = dtDtl.Rows[i]["FStockQty"].ToString();

                    cmdD.Parameters["@FUnitID"].Value = dtDtl.Rows[i]["FUnitID"].ToString();
                    cmdD.Parameters["@FDCStockID"].Value = dtDtl.Rows[i]["FDCStockID"].ToString();
                    cmdD.Parameters["@FDCSPID"].Value = dtDtl.Rows[i]["FDCSPID"].ToString();
                    cmdD.Parameters["@FSourceBillNo"].Value = dtDtl.Rows[i]["FSourceBillNo"].ToString();
                    cmdD.Parameters["@FSourceInterId"].Value = dtDtl.Rows[i]["FSourceInterId"].ToString();

                    cmdD.Parameters["@FSourceEntryID"].Value = dtDtl.Rows[i]["FSourceEntryID"].ToString();
                    cmdD.Parameters["@Fprice"].Value = dtDtl.Rows[i]["Fprice"].ToString();
                    cmdD.Parameters["@FAmount"].Value = dtDtl.Rows[i]["FAmount"].ToString();
                    cmdD.Parameters["@FNote"].Value = dtDtl.Rows[i]["FNote"].ToString();

                    strSQL = @"INSERT INTO dbo.ICstockbillentry(FInterID,FEntryID,FBrNo,FItemID,FBatchNo,FQty,FQtyMust,FUnitID,FAuxQtyMust,Fauxqty, FDCStockID,FDCSPID,FSourceTranType,FSourceBillNo,FSourceInterId,FSourceEntryID,FOrderBillno,FOrderInterID,FOrderEntryID,FOrgBillEntryID,    FChkPassItem,Fconsignprice,FconsignAmount,Fprice,Fauxprice,FAmount,FNote)
                    VALUES(@FInterID,@FEntryID,'0',@FItemID,@FBatchNo,@FQty,@FQty,@FUnitID,@FQty,@FQty, @FDCStockID,@FDCSPID,71,@FSourceBillNo,@FSourceInterId,@FSourceEntryID,@FSourceBillNo,@FSourceInterId,@FSourceEntryID,@FSourceEntryID,    1058,@Fprice,@FAmount,@Fprice,@Fprice,@FAmount,@FNote)";

                    cmdD.CommandText = strSQL;
                    cmdD.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    //删除已生成的单据数据
                    SqlOperation(0, "DELETE FROM ICStockBill WHERE FInterID = " + FInterID.ToString() + " DELETE FROM ICstockbillentry WHERE FInterID = " + FInterID.ToString());

                    conn.Close();
                    return "no@x004:" + ex.Message;
                }
            }
            #endregion

            #region 反写库存和采购订单
            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                if (dtDtl.Rows[i]["FDCSPID"] == null || dtDtl.Rows[i]["FDCSPID"].ToString() == "0")
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
                        SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FDCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FDCSPID"].ToString() + " FSPID," + dtDtl.Rows[i]["FStockQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty + DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);
                    UPDATE POOrderEntry SET FStockQty =  FStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FAuxStockQty =  FAuxStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FSecStockQty =  FSecStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FCommitQty =  FCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FAuxCommitQty = FAuxCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FSecCommitQty =  FSecCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FSourceInterId"].ToString() + " AND FEntryID = " + dtDtl.Rows[i]["FSourceEntryID"].ToString() + ";";
                else
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
                        SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FDCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FDCSPID"].ToString() + " FSPID," + dtDtl.Rows[i]["FStockQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty + DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);
                    UPDATE POOrderEntry SET FStockQty =  FStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FAuxStockQty =  FAuxStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FSecStockQty =  FSecStockQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FCommitQty =  FCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FAuxCommitQty = FAuxCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + ",FSecCommitQty =  FSecCommitQty + " + dtDtl.Rows[i]["FStockQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FSourceInterId"].ToString() + " AND FEntryID = " + dtDtl.Rows[i]["FSourceEntryID"].ToString() + ";";

                SqlOperation(0, strSQL);
            }
            #endregion

            if (conn.State == ConnectionState.Open)
                conn.Close();

            return "yes@ID:" + FInterID.ToString() + ";Number:" + FBillNo;
        }
        #endregion

        #region 外购入库单2
        /// <summary>
        /// 外购入库单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FPOOrderBillNo|FNote</param>
        /// <param name="pDetails">表体：[FItemNumber|FDCStockNumber|FDCSPNumber|FBatchNo|FQty|FSourceBillNo|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        public static string ICStockBillForPO2(string pHead, string pDetails)
        {
            object obj;
            string strSQL;
            DataTable dtDtl;
            DataRow dr;
            SqlConnection conn;

            int FInterID;
            string FBillNo;

            GetICMaxIDAndBillNo(1, out FInterID, out FBillNo);

            if (FBillNo.IndexOf("Error") >= 0)
                return "no@x001:" + FBillNo;

            //POOrder
            int POFInterID, POFEntryID;
            DataTable dtCheck;

            //定义表头字段
            string FNote, FPOOrderBillNo;
            int FDeptID, FSupplyID, FSManagerID, FFManagerID, FBillerID, POFInterIDH;

            //定义表体字段
            int FItemID, FUnitID, FDCStockID, FDCSPID, FSEQ;
            decimal FPrice, FQty, FStockQty;
            string FNoteD, FItem, FDCStock, FDCSP, FBatchNo, FSourceBillNo;

            //物料
            bool FBatchManager = false;

            #region 字段赋值
            try
            {
                FDeptID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FDeptID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FSManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FFManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FFManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FBillerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FBillerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FPOOrderBillNo = pHead.Substring(0, pHead.IndexOf("|"));//FPOOrderBillNo
                //
                FNote = pHead.Substring(pHead.IndexOf("|") + 1);//FNote

                obj = SqlOperation(3, "SELECT FInterID,FSupplyID FROM POOrder WHERE FBillNo = '" + FPOOrderBillNo + "'");
                if (obj == null || ((DataTable)obj).Rows.Count == 0)
                    return "no@没有此采购订单数据[" + FPOOrderBillNo + "]";

                POFInterIDH = int.Parse(((DataTable)obj).Rows[0]["FInterID"].ToString());
                FSupplyID = int.Parse(((DataTable)obj).Rows[0]["FSupplyID"].ToString());

                //
                dtDtl = new DataTable();
                dtDtl.Columns.Add("FItem");
                dtDtl.Columns.Add("FItemID");//物料ID
                dtDtl.Columns.Add("FUnitID");//单位ID
                dtDtl.Columns.Add("FDCStockID");//仓库
                dtDtl.Columns.Add("FDCSPID");//仓位

                dtDtl.Columns.Add("FBatchNo");//批次号
                dtDtl.Columns.Add("FQty");//本行入库数量
                dtDtl.Columns.Add("Fprice");//单价
                dtDtl.Columns.Add("FAmount");//总金额
                dtDtl.Columns.Add("FSourceBillNo");//采购订单号

                dtDtl.Columns.Add("FSourceInterId");//采购订单内码
                dtDtl.Columns.Add("FSourceEntryID");//采购订单分录内码
                dtDtl.Columns.Add("FNote");//备注
                dtDtl.Columns.Add("FStockQty");//未入库总数量
                dtDtl.Columns.Add("TotalQty");//已入库总数量
                dtDtl.Columns.Add("FSEQ");//重复行数

                string strTemp;
                do
                {
                    if (pDetails.IndexOf("],[") > 0)
                    {
                        strTemp = pDetails.Substring(0, pDetails.IndexOf("]") + 1);
                        pDetails = pDetails.Substring(pDetails.IndexOf("]") + 2);
                    }
                    else
                    {
                        strTemp = pDetails;
                        pDetails = "";
                    }

                    FItem = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//物料
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//仓库
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//仓位
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//批次号
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//入库数量

                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSourceBillNo = strTemp.Substring(0, strTemp.IndexOf("|"));//采购订单号
                    //
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FNoteD = strTemp.Substring(0, strTemp.IndexOf("]"));//备注

                    //strSQL = @"SELECT A.FInterID FSourceInterId,A.FSupplyID,AE.FEntryID FSourceEntryID,AE.FItemID,MTL.FUnitID,AE.FPrice,AE.FQty - AE.FStockQty FStockQty,MTL.FBatchManager
                    //FROM POOrder A
                    //INNER JOIN POOrderEntry AE ON A.FInterID = AE.FInterID
                    //INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
                    //WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + @"'";

                    //strSQL = @"SELECT A.FInterID FSourceInterId,A.FSupplyID,AE.FItemID,MTL.FUnitID,AE.FPrice,SUM(AE.FQty - AE.FStockQty) FStockQty,MTL.FBatchManager,COUNT(*) FSEQ
                    //FROM POOrder A
                    //INNER JOIN POOrderEntry AE ON A.FInterID = AE.FInterID
                    //INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
                    //WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + @"'
                    //GROUP BY A.FInterID,A.FSupplyID,AE.FItemID,MTL.FUnitID,AE.FPrice,MTL.FBatchManager";

                    strSQL = @"SELECT A.FInterID FSourceInterId,AE.FEntryID FSourceEntryID,A.FSupplyID,AE.FItemID,MTL.FUnitID,AE.FPrice,MTL.FBatchManager,O.FStockQty,O.FSEQ
                    FROM POOrder A
                    INNER JOIN POOrderEntry AE ON A.FInterID = AE.FInterID
                    INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID
                    INNER JOIN 
                    (
                    SELECT FInterID,FItemID,SUM(FQty - FStockQty) FStockQty,COUNT(*) FSEQ
                    FROM POOrderEntry
                    GROUP BY FInterID,FItemID
                    )O ON O.FInterID = AE.FInterID AND O.FItemID = AE.FItemID
                    WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + "'";

                    obj = SqlOperation(3, strSQL);
                    if (obj == null || ((DataTable)obj).Rows.Count == 0)
                        return "no@未找到物料信息[" + FSourceBillNo + "].[" + FItem + "]";

                    POFInterID = int.Parse(((DataTable)obj).Rows[0]["FSourceInterId"].ToString());//FInterID
                    POFEntryID = int.Parse(((DataTable)obj).Rows[0]["FSourceEntryID"].ToString());//FEntryID
                    FItemID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//MTLID
                    FUnitID = int.Parse(((DataTable)obj).Rows[0]["FUnitID"].ToString());//UnitID
                    FPrice = decimal.Parse(((DataTable)obj).Rows[0]["FPrice"].ToString());//Price
                    FStockQty = decimal.Parse(((DataTable)obj).Rows[0]["FStockQty"].ToString());//未入库总数量
                    FSEQ = int.Parse(((DataTable)obj).Rows[0]["FSEQ"].ToString());//重复行数

                    FBatchManager = ((DataTable)obj).Rows[0]["FBatchManager"].ToString() == "0" ? false : true;//是否采用业务批次管理

                    if (FSupplyID != int.Parse(((DataTable)obj).Rows[0]["FSupplyID"].ToString()))
                    {
                        return "no@表头[" + FPOOrderBillNo + "]的供应商与表体[" + FSourceBillNo + "]的供应商不一致。";
                    }

                    if (FQty > FStockQty)
                    {
                        return "no@[" + FSourceBillNo + "].[" + FItem + "]物料本次入库数量[" + FQty.ToString() + "]大于采购订单的未入库总数量[" + FStockQty.ToString() + "]。";
                    }

                    if (FDCStock == "")
                        FDCStockID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓库信息[" + FDCStock + "]";
                        FDCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FStockID
                    }

                    if (FDCSP == "")
                        FDCSPID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FSPID FROM t_StockPlace WHERE FNumber = '" + FDCSP + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓位信息[" + FDCSP + "]";
                        FDCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FDCSPID
                    }

                    if (FBatchNo.Equals(string.Empty) && FBatchManager)
                    {
                        return "no@[" + FItem + "]物料已经启用批次号管理，请携带批次号。";
                    }

                    dr = dtDtl.NewRow();
                    dr["FItem"] = FItem;//物料编码

                    dr["FItemID"] = FItemID;//物料ID
                    dr["FUnitID"] = FUnitID;//单位ID
                    dr["FDCStockID"] = FDCStockID;//仓库ID
                    dr["FDCSPID"] = FDCSPID;//仓位ID
                    dr["FBatchNo"] = FBatchNo;//批次号

                    dr["FQty"] = FQty;//本行入库数量
                    dr["Fprice"] = FPrice;//单价
                    dr["FAmount"] = FQty * FPrice;//金额
                    dr["FSourceBillNo"] = FSourceBillNo;//采购订单
                    dr["FSourceInterId"] = POFInterID;//采购订单内码

                    dr["FSourceEntryID"] = POFEntryID;//采购订单明显内码
                    dr["FNote"] = FNoteD;//备注
                    dr["FStockQty"] = FStockQty;//未入库总数量
                    dr["TotalQty"] = FQty;//已入库总数量*
                    dr["FSEQ"] = FSEQ;//重复行数*

                    dtDtl.Rows.Add(dr);
                }
                while (pDetails.Length > 0);
            }
            catch (Exception ex)
            {
                return "no@x002:" + ex.Message;
            }
            #endregion

            #region 对入库数量的判断
            dtCheck = dtDtl.Clone();//获取汇总FQty的物料信息
            dtCheck.ImportRow(dtDtl.Rows[0]);//把第一条数据写入dtPO中
            //汇总入库情况
            for (int i = 1; i < dtDtl.Rows.Count; i++)
            {
                for (int j = 0; j < dtCheck.Rows.Count; j++)//根据采购单号与物料编码进行匹配
                {
                    if (dtCheck.Rows[j]["FSourceBillNo"] == dtDtl.Rows[i]["FSourceBillNo"] && dtCheck.Rows[j]["FItem"] == dtDtl.Rows[i]["FItem"])
                    {
                        dtCheck.Rows[j]["FQty"] = decimal.Parse(dtCheck.Rows[j]["FQty"].ToString()) + decimal.Parse(dtDtl.Rows[i]["FQty"].ToString());
                        break;
                    }

                    if (j == dtCheck.Rows.Count - 1)//未匹配到数据
                    {
                        dtCheck.ImportRow(dtDtl.Rows[i]);
                        break;
                    }
                }
            }

            for (int i = 0; i < dtCheck.Rows.Count; i++)
            {
                if (decimal.Parse(dtCheck.Rows[i]["FQty"].ToString()) > decimal.Parse(dtCheck.Rows[i]["FStockQty"].ToString()))//入库总数量大于未入库总数量
                {
                    return "no@[" + dtCheck.Rows[i]["FSourceBillNo"].ToString() + "].[" + dtCheck.Rows[i]["FItem"].ToString() + "]物料本次总入库数量[" + dtCheck.Rows[i]["FQty"].ToString() + "]大于未入库总数量[" + dtCheck.Rows[i]["FStockQty"].ToString() + "]。";
                }
            }
            #endregion

            conn = new SqlConnection(C_CONNECTIONSTRING);

            #region 插入主表
            try
            {
                conn.Open();
                SqlCommand cmdH = conn.CreateCommand();
                cmdH.CommandType = CommandType.Text;

                cmdH.Parameters.Add("@FInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillNo", SqlDbType.NVarChar, 255);
                cmdH.Parameters.Add("@FNote", SqlDbType.NVarChar, 255);
                cmdH.Parameters.Add("@FDeptID", SqlDbType.Int);
                cmdH.Parameters.Add("@FSupplyID", SqlDbType.Int);

                cmdH.Parameters.Add("@FSManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FFManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FOrgBillInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FPOOrderBillNo", SqlDbType.NVarChar);

                cmdH.Parameters["@FInterID"].Value = FInterID;
                cmdH.Parameters["@FBillNo"].Value = FBillNo;
                cmdH.Parameters["@FNote"].Value = FNote;
                cmdH.Parameters["@FDeptID"].Value = FDeptID;
                cmdH.Parameters["@FSupplyID"].Value = FSupplyID;

                cmdH.Parameters["@FSManagerID"].Value = FSManagerID;
                cmdH.Parameters["@FFManagerID"].Value = FFManagerID;
                cmdH.Parameters["@FBillerID"].Value = FBillerID;
                cmdH.Parameters["@FOrgBillInterID"].Value = POFInterIDH;
                cmdH.Parameters["@FPOOrderBillNo"].Value = FPOOrderBillNo;

                strSQL = @"INSERT INTO dbo.ICStockBill(FInterID,FBillNo,FBrNo,FTranType,FROB,FNote,FDate,FDeptID,FSupplyID,FPurposeID,   FSManagerID,FFManagerID,FBillerID,FCheckerID,FCheckDate,FStatus,FSelTranType,FPOMode,FPOStyle,FOrgBillInterID,FPOOrdBillNo) 
                VALUES (@FInterID,@FBillNo,'0',1,1,@FNote,CONVERT(VARCHAR(10),GETDATE(),120),@FDeptID,@FSupplyID,0,  @FSManagerID,@FFManagerID,@FBillerID,@FBillerID,GETDATE(),1,71,36680,252,@FOrgBillInterID,@FPOOrderBillNo)";

                cmdH.CommandText = strSQL;
                cmdH.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                return "no@x003:" + ex.Message;
            }
            finally
            {
                conn.Close();
            }
            #endregion

            #region 合并、拆分Dtl
            DataTable dtMS = dtDtl.Clone();//合并拆分 DataTable
            DataTable dtTemp;
            decimal TotalQty, TotalStock;

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                if (int.Parse(dtDtl.Rows[i]["FSEQ"].ToString()) > 1)//有重复物料
                {
                    dtTemp = new DataTable();//获取每行分录号及每行未入库数量
                    dtTemp = (DataTable)SqlOperation(3, "SELECT AE.FEntryID,AE.FItemID,AE.FQty,AE.FQty - AE.FStockQty FStockQty FROM POOrder A INNER JOIN POOrderEntry AE ON A.FInterID = AE.FInterID WHERE A.FBillNo = '" + dtDtl.Rows[i]["FSourceBillNo"].ToString() + "' AND AE.FItemID = " + dtDtl.Rows[i]["FItemID"].ToString() + " ORDER BY AE.FEntryID");

                    if (!ContainValue(dtMS, "FItem", dtDtl.Rows[i]["FItem"].ToString()))//本次调用接口 是否之前dtDtl中已经有相同物料入库
                        for (int j = 0; j < dtTemp.Rows.Count; j++)
                        {
                            if (decimal.Parse(dtTemp.Rows[j]["FStockQty"].ToString()) <= 0) continue;//没有未入库数量，跳过本行入库

                            if (decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()) <= decimal.Parse(dtTemp.Rows[j]["FStockQty"].ToString()))//本次剩余入库数量<=物料第J行的未入库数量
                            {
                                dtDtl.Rows[i]["FSourceEntryID"] = dtTemp.Rows[j]["FEntryID"].ToString();//获取分录号
                                dtDtl.Rows[i]["FAmount"] = decimal.Parse(dtDtl.Rows[i]["Fprice"].ToString()) * decimal.Parse(dtDtl.Rows[i]["FQty"].ToString());
                                dtDtl.Rows[i]["TotalQty"] = decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()) + SumPre(dtTemp, "FQty", j);//已入库总数=前入库总数量+本次入库数量
                                dtMS.ImportRow(dtDtl.Rows[i]);

                                UpdateTable(dtMS, "FItem", dtDtl.Rows[i]["FItem"].ToString(), "TotalQty", decimal.Parse(dtDtl.Rows[i]["TotalQty"].ToString()));
                                break;
                            }
                            else//拆分dtDtl[i]
                            {
                                dr = dtMS.NewRow();
                                dr["FItem"] = dtDtl.Rows[i]["FItem"];

                                dr["FItemID"] = dtDtl.Rows[i]["FItemID"];
                                dr["FUnitID"] = dtDtl.Rows[i]["FUnitID"];
                                dr["FDCStockID"] = dtDtl.Rows[i]["FDCStockID"];
                                dr["FDCSPID"] = dtDtl.Rows[i]["FDCSPID"];
                                dr["FBatchNo"] = dtDtl.Rows[i]["FBatchNo"];

                                dr["FQty"] = dtTemp.Rows[j]["FStockQty"];
                                dr["Fprice"] = dtDtl.Rows[i]["Fprice"];
                                dr["FAmount"] = decimal.Parse(dr["FQty"].ToString()) * decimal.Parse(dr["Fprice"].ToString());
                                dr["FSourceBillNo"] = dtDtl.Rows[i]["FSourceBillNo"];
                                dr["FSourceInterId"] = dtDtl.Rows[i]["FSourceInterId"];

                                dr["FSourceEntryID"] = dtTemp.Rows[j]["FEntryID"];
                                dr["FNote"] = dtDtl.Rows[i]["FNote"];
                                dr["FStockQty"] = dtDtl.Rows[i]["FStockQty"];
                                dr["TotalQty"] = SumPre(dtTemp, "FQty", j + 1);
                                dr["FSEQ"] = dtDtl.Rows[i]["FSEQ"];

                                dtMS.Rows.Add(dr);
                                UpdateTable(dtMS, "FItem", dtDtl.Rows[i]["FItem"].ToString(), "TotalQty", decimal.Parse(dr["TotalQty"].ToString()));
                                dtDtl.Rows[i]["FQty"] = decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()) - decimal.Parse(dtTemp.Rows[j]["FStockQty"].ToString());
                            }
                        }
                    else//本次调用接口 物料曾入库
                        for (int j = 0; j < dtTemp.Rows.Count; j++)//逐条分录入库
                        {
                            TotalStock = SumPre(dtTemp, "FQty", j);//获取dtTemp前J-1行的未入库数量和
                            TotalQty = GetTotalQty(dtMS, dtDtl.Rows[i]["FItem"].ToString());//前入库总数量
                            if (decimal.Parse(dtTemp.Rows[j]["FStockQty"].ToString()) <= 0 || decimal.Parse(dtTemp.Rows[j]["FStockQty"].ToString()) <= TotalQty - TotalStock) continue;//没有未入库数量 或 第J行未入库数量<=前入库总数量,跳过本行入库。

                            //从第J行开始入库
                            if (decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()) <= decimal.Parse(dtTemp.Rows[j]["FStockQty"].ToString()) - (TotalQty - TotalStock))//本次入库数量<=第J行剩余未入数量
                            {
                                dtDtl.Rows[i]["FSourceEntryID"] = dtTemp.Rows[j]["FEntryID"];
                                dtDtl.Rows[i]["FAmount"] = decimal.Parse(dtDtl.Rows[i]["Fprice"].ToString()) * decimal.Parse(dtDtl.Rows[i]["FQty"].ToString());
                                dtDtl.Rows[i]["TotalQty"] = decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()) + TotalQty;
                                dtMS.ImportRow(dtDtl.Rows[i]);

                                UpdateTable(dtMS, "FItem", dtDtl.Rows[i]["FItem"].ToString(), "TotalQty", decimal.Parse(dtDtl.Rows[i]["TotalQty"].ToString()));
                                break;
                            }
                            else//本次入库数量>第J行剩余未入数量 拆分dtDtl[i]
                            {
                                dr = dtMS.NewRow();
                                dr["FItem"] = dtDtl.Rows[i]["FItem"];

                                dr["FItemID"] = dtDtl.Rows[i]["FItemID"];
                                dr["FUnitID"] = dtDtl.Rows[i]["FUnitID"];
                                dr["FDCStockID"] = dtDtl.Rows[i]["FDCStockID"];
                                dr["FDCSPID"] = dtDtl.Rows[i]["FDCSPID"];
                                dr["FBatchNo"] = dtDtl.Rows[i]["FBatchNo"];

                                dr["FQty"] = decimal.Parse(dtTemp.Rows[j]["FStockQty"].ToString()) - (TotalQty - TotalStock);//本行入库数量
                                dr["Fprice"] = dtDtl.Rows[i]["Fprice"];
                                dr["FAmount"] = decimal.Parse(dr["FQty"].ToString()) * decimal.Parse(dr["Fprice"].ToString());
                                dr["FSourceBillNo"] = dtDtl.Rows[i]["FSourceBillNo"];
                                dr["FSourceInterId"] = dtDtl.Rows[i]["FSourceInterId"];

                                dr["FSourceEntryID"] = dtTemp.Rows[j]["FEntryID"];//对应源单分录号入库
                                dr["FNote"] = dtDtl.Rows[i]["FNote"];
                                dr["TotalQty"] = SumPre(dtTemp, "FQty", j + 1);//TotalQty + decimal.Parse(dtTemp.Rows[j]["FStockQty"].ToString());//已入库总数量
                                dr["FStockQty"] = decimal.Parse(dtDtl.Rows[0]["FStockQty"].ToString()) - decimal.Parse(dr["TotalQty"].ToString());//未入库总数量
                                dr["FSEQ"] = dtDtl.Rows[i]["FSEQ"];

                                dtMS.Rows.Add(dr);

                                UpdateTable(dtMS, "FItem", dtDtl.Rows[i]["FItem"].ToString(), "TotalQty", decimal.Parse(dr["TotalQty"].ToString()));
                                dtDtl.Rows[i]["FQty"] = decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()) - (decimal.Parse(dtTemp.Rows[j]["FStockQty"].ToString()) - (TotalQty - TotalStock));
                            }
                        }
                }
                else dtMS.ImportRow(dtDtl.Rows[i]);//没有重复物料
            }
            #endregion

            #region 插入表体
            conn.Open();
            SqlCommand cmdD = conn.CreateCommand();
            cmdD.CommandType = CommandType.Text;

            cmdD.Parameters.Add("@FInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@FEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@FItemID", SqlDbType.Int);
            cmdD.Parameters.Add("@FBatchNo", SqlDbType.VarChar);
            cmdD.Parameters.Add("@FQty", SqlDbType.Decimal);

            cmdD.Parameters.Add("@FUnitID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCSPID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSourceBillNo", SqlDbType.VarChar, 50);
            cmdD.Parameters.Add("@FSourceInterId", SqlDbType.Int);

            cmdD.Parameters.Add("@FSourceEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@Fprice", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FAmount", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FNote", SqlDbType.VarChar);

            for (int i = 0; i < dtMS.Rows.Count; i++)
            {
                try
                {
                    cmdD.Parameters["@FInterID"].Value = FInterID;
                    cmdD.Parameters["@FEntryID"].Value = i + 1;
                    cmdD.Parameters["@FItemID"].Value = dtMS.Rows[i]["FItemID"].ToString();
                    cmdD.Parameters["@FBatchNo"].Value = dtMS.Rows[i]["FBatchNo"].ToString();
                    cmdD.Parameters["@FQty"].Value = dtMS.Rows[i]["FQty"].ToString();

                    cmdD.Parameters["@FUnitID"].Value = dtMS.Rows[i]["FUnitID"].ToString();
                    cmdD.Parameters["@FDCStockID"].Value = dtMS.Rows[i]["FDCStockID"].ToString();
                    cmdD.Parameters["@FDCSPID"].Value = dtMS.Rows[i]["FDCSPID"].ToString();
                    cmdD.Parameters["@FSourceBillNo"].Value = dtMS.Rows[i]["FSourceBillNo"].ToString();
                    cmdD.Parameters["@FSourceInterId"].Value = dtMS.Rows[i]["FSourceInterId"].ToString();

                    cmdD.Parameters["@FSourceEntryID"].Value = dtMS.Rows[i]["FSourceEntryID"].ToString();
                    cmdD.Parameters["@Fprice"].Value = dtMS.Rows[i]["Fprice"].ToString();
                    cmdD.Parameters["@FAmount"].Value = dtMS.Rows[i]["FAmount"].ToString();
                    cmdD.Parameters["@FNote"].Value = dtMS.Rows[i]["FNote"].ToString();

                    strSQL = @"INSERT INTO dbo.ICstockbillentry(FInterID,FEntryID,FBrNo,FItemID,FBatchNo,FQty,FQtyMust,FUnitID,FAuxQtyMust,Fauxqty, FDCStockID,FDCSPID,FSourceTranType,FSourceBillNo,FSourceInterId,FSourceEntryID,FOrderBillno,FOrderInterID,FOrderEntryID,FOrgBillEntryID,    FChkPassItem,Fconsignprice,FconsignAmount,Fprice,Fauxprice,FAmount,FNote)
                    VALUES(@FInterID,@FEntryID,'0',@FItemID,@FBatchNo,@FQty,@FQty,@FUnitID,@FQty,@FQty, @FDCStockID,@FDCSPID,71,@FSourceBillNo,@FSourceInterId,@FSourceEntryID,@FSourceBillNo,@FSourceInterId,@FSourceEntryID,@FSourceEntryID,    1058,@Fprice,@FAmount,@Fprice,@Fprice,@FAmount,@FNote)";

                    cmdD.CommandText = strSQL;
                    cmdD.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    //删除已生成的单据数据
                    SqlOperation(0, "DELETE FROM ICStockBill WHERE FInterID = " + FInterID.ToString() + " DELETE FROM ICstockbillentry WHERE FInterID = " + FInterID.ToString());

                    conn.Close();
                    return "no@x004:" + ex.Message;
                }
            }
            #endregion

            #region 反写库存和采购订单
            for (int i = 0; i < dtMS.Rows.Count; i++)
            {
                if (dtMS.Rows[i]["FDCSPID"] == null || dtMS.Rows[i]["FDCSPID"].ToString() == "0")
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
                        SELECT " + dtMS.Rows[i]["FItemID"].ToString() + " FItemID, " + dtMS.Rows[i]["FDCStockID"].ToString() + " FStockID," + dtMS.Rows[i]["FDCSPID"].ToString() + " FSPID," + dtMS.Rows[i]["FQty"].ToString() + " FQty,'" + dtMS.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty + DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);
                    UPDATE POOrderEntry SET FStockQty =  FStockQty + " + dtMS.Rows[i]["FQty"].ToString() + ",FAuxStockQty =  FAuxStockQty + " + dtMS.Rows[i]["FQty"].ToString() + ",FSecStockQty =  FSecStockQty + " + dtMS.Rows[i]["FQty"].ToString() + ",FCommitQty =  FCommitQty + " + dtMS.Rows[i]["FQty"].ToString() + ",FAuxCommitQty = FAuxCommitQty + " + dtMS.Rows[i]["FQty"].ToString() + ",FSecCommitQty =  FSecCommitQty + " + dtMS.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtMS.Rows[i]["FSourceInterId"].ToString() + " AND FEntryID = " + dtMS.Rows[i]["FSourceEntryID"].ToString() + ";";
                else
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
                        SELECT " + dtMS.Rows[i]["FItemID"].ToString() + " FItemID, " + dtMS.Rows[i]["FDCStockID"].ToString() + " FStockID," + dtMS.Rows[i]["FDCSPID"].ToString() + " FSPID," + dtMS.Rows[i]["FQty"].ToString() + " FQty,'" + dtMS.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty + DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);
                    UPDATE POOrderEntry SET FStockQty =  FStockQty + " + dtMS.Rows[i]["FQty"].ToString() + ",FAuxStockQty =  FAuxStockQty + " + dtMS.Rows[i]["FQty"].ToString() + ",FSecStockQty =  FSecStockQty + " + dtMS.Rows[i]["FQty"].ToString() + ",FCommitQty =  FCommitQty + " + dtMS.Rows[i]["FQty"].ToString() + ",FAuxCommitQty = FAuxCommitQty + " + dtMS.Rows[i]["FQty"].ToString() + ",FSecCommitQty =  FSecCommitQty + " + dtMS.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtMS.Rows[i]["FSourceInterId"].ToString() + " AND FEntryID = " + dtMS.Rows[i]["FSourceEntryID"].ToString() + ";";

                SqlOperation(0, strSQL);
            }
            #endregion

            if (conn.State == ConnectionState.Open)
                conn.Close();

            return "yes@ID:" + FInterID.ToString() + ";Number:" + FBillNo;
        }
        #endregion

        #region 生产领料单
        /// <summary>
        /// 生产领料单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FNote</param>
        /// <param name="pDetails">表体：[FSCStockNumber|FDCSPNumber|FBatchNo|FQty|FSourceBillNo|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        public static string ICStockBillForPick(string pHead, string pDetails)
        {
            object obj;
            string strSQL;
            DataTable dtQty, dtCheck;
            DataTable dtDtl;
            DataRow dr;
            SqlConnection conn;

            int FInterID;
            string FBillNo;

            GetICMaxIDAndBillNo(24, out FInterID, out FBillNo);

            if (FBillNo.IndexOf("Error") >= 0)
                return "no@x001:" + FBillNo;

            //ICMO
            int MOFInterID;

            //定义表头字段
            int FDeptID, FSManagerID, FFManagerID, FBillerID;
            string FNote;

            //定义表体字段
            int FItemID, FUnitID, FSCStockID, FDCSPID, FCostObjID, FDetailID, FPPBomID, FPPBomEntryID;
            decimal FQty, FStockQty;
            string FItem, FSCStock, FDCSP, FBatchNo = string.Empty, FSourceBillNo, FNoteD;

            //物料
            bool FBatchManager = false;

            #region 字段赋值
            try
            {
                FDeptID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FDeptID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FSManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FFManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FFManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FBillerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FBillerID

                FNote = pHead.Substring(pHead.IndexOf("|") + 1);//FNote

                //
                dtDtl = new DataTable();
                dtDtl.Columns.Add("FItemID");//物料ID
                dtDtl.Columns.Add("FUnitID");//单位ID
                dtDtl.Columns.Add("FSCStockID");//仓库ID
                dtDtl.Columns.Add("FDCSPID");//仓位ID
                dtDtl.Columns.Add("FBatchNo");//批次号

                dtDtl.Columns.Add("FQty");//本次领料数量
                dtDtl.Columns.Add("FNote");//备注
                dtDtl.Columns.Add("FCostObjID");//成本对象
                dtDtl.Columns.Add("FSourceBillNo");//源单编码
                dtDtl.Columns.Add("FSourceInterId");//源单内码

                dtDtl.Columns.Add("FDetailID");//分录号
                dtDtl.Columns.Add("FPPBomID");//物料清单ID
                dtDtl.Columns.Add("FPPBomEntryID");//物料清单明细ID
                dtDtl.Columns.Add("FStockQty");//未领数量
                dtDtl.Columns.Add("FItem");//物料编码

                string strTemp;
                do
                {
                    if (pDetails.IndexOf("],[") > 0)
                    {
                        strTemp = pDetails.Substring(0, pDetails.IndexOf("]") + 1);
                        pDetails = pDetails.Substring(pDetails.IndexOf("]") + 2);
                    }
                    else
                    {
                        strTemp = pDetails;
                        pDetails = "";
                    }

                    FItem = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//MTLNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//StockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY

                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSourceBillNo = strTemp.Substring(0, strTemp.IndexOf("|"));//ICMOBillNo
                    //
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FNoteD = strTemp.Substring(0, strTemp.IndexOf("]"));//FNoteD

                    strSQL = @"SELECT A.FInterID FSourceInterId,BOME.FItemID,MTL.FUnitID,IT2.FItemID FCostOBJID,BOM.FInterID FPPBomID,BOME.FEntryID FPPBomEntryID,BOME.FDetailID,(BOME.FQtyMust - BOME.FStockQty) FStockQty,MTL.FBatchManager
                    FROM ICMO A
                    INNER JOIN PPBOM BOM ON A.FInterID = BOM.FICMOInterID
                    INNER JOIN PPBOMEntry BOME ON BOM.FInterID = BOME.FInterID
                    INNER JOIN t_ICItem MTL ON BOME.FItemID = MTL.FItemID
                    INNER JOIN t_Item IT ON A.FItemID = IT.FItemID
                    INNER JOIN t_Item IT2 ON IT.FNumber = IT2.Fnumber AND IT2.FItemClassID = 2001
                    WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + "'";

                    obj = SqlOperation(3, strSQL);
                    if (obj == null || ((DataTable)obj).Rows.Count == 0)
                        return "no@未找到单据信息[" + FSourceBillNo + "].[" + FItem + "]";

                    MOFInterID = int.Parse(((DataTable)obj).Rows[0]["FSourceInterId"].ToString());//FInterID
                    FItemID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//MTLID
                    FUnitID = int.Parse(((DataTable)obj).Rows[0]["FUnitID"].ToString());//UnitID
                    FCostObjID = int.Parse(((DataTable)obj).Rows[0]["FCostOBJID"].ToString());//FCostObjID
                    FPPBomID = int.Parse(((DataTable)obj).Rows[0]["FPPBomID"].ToString());//FPPBomID

                    FPPBomEntryID = int.Parse(((DataTable)obj).Rows[0]["FPPBomEntryID"].ToString());//FPPBomEntryID
                    FDetailID = int.Parse(((DataTable)obj).Rows[0]["FDetailID"].ToString());//FDetailID
                    FBatchManager = ((DataTable)obj).Rows[0]["FBatchManager"].ToString() == "0" ? false : true;//是否采用业务批次管理
                    FStockQty = decimal.Parse(((DataTable)obj).Rows[0]["FStockQty"].ToString());//FStockQty

                    if (FQty > FStockQty)
                    {
                        return "no@物料[" + FItem + "]本次领料数量[" + FQty.ToString() + "]大于未领数量[" + FStockQty.ToString() + "]";
                    }

                    if (FSCStock == "")
                        FSCStockID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FSCStock + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓库信息[" + FSCStock + "]";
                        FSCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FSCStockID
                    }

                    if (FDCSP == "")
                        FDCSPID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FSPID FROM t_StockPlace WHERE FNumber = '" + FDCSP + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓位信息[" + FDCSP + "]";
                        FDCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FDCSPID
                    }

                    if (FBatchNo.Equals(string.Empty) && FBatchManager)
                    {
                        return "no@[" + FItem + "]物料已经启用批次号管理，请携带批次号。";
                    }

                    dr = dtDtl.NewRow();

                    dr["FItemID"] = FItemID;
                    dr["FUnitID"] = FUnitID;
                    dr["FSCStockID"] = FSCStockID;
                    dr["FDCSPID"] = FDCSPID;
                    dr["FBatchNo"] = FBatchNo;

                    dr["FQty"] = FQty;
                    dr["FNote"] = FNoteD;
                    dr["FCostOBJID"] = FCostObjID;
                    dr["FSourceBillNo"] = FSourceBillNo;
                    dr["FSourceInterId"] = MOFInterID;

                    dr["FDetailID"] = FDetailID;
                    dr["FPPBomID"] = FPPBomEntryID;
                    dr["FPPBomEntryID"] = FPPBomEntryID;
                    dr["FStockQty"] = FStockQty;
                    dr["FItem"] = FItem;

                    dtDtl.Rows.Add(dr);
                }
                while (pDetails.Length > 0);
            }
            catch (Exception ex)
            {
                return "no@x002:" + ex.Message;
            }
            #endregion

            #region 判断：是否有总领料数量大于计划投料数量和是否有总领料数量大于即时库存数量

            //1、是否有总领料数量大于计划投料数量
            dtQty = dtDtl.Clone();//汇总物料本次领料总数
            dtQty.ImportRow(dtDtl.Rows[0]);

            for (int i = 1; i < dtDtl.Rows.Count; i++)
            {
                for (int j = 0; j < dtQty.Rows.Count; j++)
                {
                    if (dtQty.Rows[j]["FItemID"] == dtDtl.Rows[i]["FItemID"])
                    {
                        dtQty.Rows[j]["FQty"] = decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()) + decimal.Parse(dtQty.Rows[j]["FQty"].ToString());
                        break;
                    }

                    if (j == dtQty.Rows.Count - 1)//未匹配到数据
                    {
                        dtQty.ImportRow(dtDtl.Rows[i]);
                        break;
                    }
                }
            }
            //物料本次总领料数量与未领数量判断
            for (int i = 0; i < dtQty.Rows.Count; i++)
            {
                if (decimal.Parse(dtQty.Rows[i]["FQty"].ToString()) > decimal.Parse(dtQty.Rows[i]["FStockQty"].ToString()))
                {
                    return "no@物料[" + dtQty.Rows[i]["FItem"].ToString() + "]本次总领料数量[" + dtQty.Rows[i]["FQty"].ToString() + "]大于未领数量[" + dtQty.Rows[i]["FStockQty"].ToString() + "]";
                }
            }


            //2、是否有总领料数量大于即时库存数量
            dtCheck = dtDtl.Clone();//汇总物料本次领料总数(带仓库、仓位、批次号匹配)
            dtCheck.ImportRow(dtDtl.Rows[0]);

            for (int i = 1; i < dtDtl.Rows.Count; i++)
            {
                for (int j = 0; j < dtCheck.Rows.Count; j++)
                {
                    if (dtCheck.Rows[j]["FItemID"] == dtDtl.Rows[i]["FItemID"] && dtCheck.Rows[j]["FSCStockID"] == dtDtl.Rows[i]["FSCStockID"] && dtCheck.Rows[j]["FBatchNo"] == dtDtl.Rows[i]["FBatchNo"] && dtCheck.Rows[j]["FDCSPID"] == dtDtl.Rows[i]["FDCSPID"])
                    {
                        dtCheck.Rows[j]["FQty"] = decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()) + decimal.Parse(dtCheck.Rows[j]["FQty"].ToString());
                        break;
                    }

                    if (j == dtCheck.Rows.Count - 1)//未匹配到数据
                    {
                        dtCheck.ImportRow(dtDtl.Rows[i]);
                        break;
                    }
                }
            }
            //物料总领料数量与库存数量判断
            for (int i = 0; i < dtCheck.Rows.Count; i++)
            {
                if (dtCheck.Rows[i]["FDCSPID"].ToString() != "0")
                    strSQL = @"SELECT MTL.FNumber,CASE WHEN " + dtCheck.Rows[i]["FQty"].ToString() + @" > ISNULL(INV.FQty,0) THEN ISNULL(INV.FQty,0) ELSE -1 END FQty
                    FROM t_ICItem MTL
                    LEFT JOIN ICInventory INV ON INV.FItemID = MTL.FItemID AND INV.FStockID = " + dtCheck.Rows[i]["FSCStockID"].ToString() + " AND INV.FBatchNo = '" + dtCheck.Rows[i]["FBatchNo"].ToString() + "' AND FStockPlaceID = " + dtCheck.Rows[i]["FDCSPID"].ToString() + @"
                    WHERE MTL.FItemID = " + dtCheck.Rows[i]["FItemID"].ToString();
                else
                    strSQL = @"SELECT MTL.FNumber,CASE WHEN " + dtCheck.Rows[i]["FQty"].ToString() + @" > ISNULL(INV.FQty,0) THEN ISNULL(INV.FQty,0) ELSE -1 END FQty
                    FROM t_ICItem MTL
                    LEFT JOIN ICInventory INV ON INV.FItemID = MTL.FItemID AND INV.FStockID = " + dtCheck.Rows[i]["FSCStockID"].ToString() + " AND INV.FBatchNo = '" + dtCheck.Rows[i]["FBatchNo"].ToString() + @"'
                    WHERE MTL.FItemID = " + dtCheck.Rows[i]["FItemID"].ToString();

                obj = SqlOperation(3, strSQL);

                if (obj == null || ((DataTable)obj).Rows.Count == 0)
                    return "no@物料[" + dtCheck.Rows[i]["FItem"].ToString() + "]查询失败";
                else if (decimal.Parse(((DataTable)obj).Rows[0]["FQty"].ToString()) != -1)
                    return "no@物料[" + dtCheck.Rows[i]["FItem"].ToString() + "]本次总领料数量[" + dtCheck.Rows[i]["FQty"].ToString() + "]大于即时库存数量[" + ((DataTable)obj).Rows[0]["FQty"].ToString() + "]";
            }
            #endregion

            conn = new SqlConnection(C_CONNECTIONSTRING);

            #region 插入表头
            try
            {
                conn.Open();
                SqlCommand cmdH = conn.CreateCommand();
                cmdH.CommandType = CommandType.Text;

                cmdH.Parameters.Add("@FInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillNo", SqlDbType.NVarChar, 255);
                cmdH.Parameters.Add("@FDeptID", SqlDbType.Int);
                cmdH.Parameters.Add("@FSManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FFManagerID", SqlDbType.Int);

                cmdH.Parameters.Add("@FBillerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FNote", SqlDbType.NVarChar);

                cmdH.Parameters["@FInterID"].Value = FInterID;
                cmdH.Parameters["@FBillNo"].Value = FBillNo;
                cmdH.Parameters["@FDeptID"].Value = FDeptID;
                cmdH.Parameters["@FSManagerID"].Value = FSManagerID;
                cmdH.Parameters["@FFManagerID"].Value = FFManagerID;

                cmdH.Parameters["@FBillerID"].Value = FBillerID;
                cmdH.Parameters["@FNote"].Value = FNote;

                strSQL = @"INSERT INTO dbo.ICStockBill(FInterID,FBillNo,FBrNo,FTranType,FROB,FDate,FDeptID,FPurposeID,FSManagerID,FFManagerID,  FBillerID,FSelTranType,FPOMode,FPOStyle,FCussentAcctID,FNote) 
                VALUES (@FInterID,@FBillNo,'0',24,1,CONVERT(VARCHAR(10),GETDATE(),120),@FDeptID,12000,@FSManagerID,@FFManagerID,   @FBillerID,85,36680,252,1020,@FNote)";

                cmdH.CommandText = strSQL;
                cmdH.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                return "no@x003:" + ex.Message;
            }
            finally
            {
                conn.Close();
            }
            #endregion

            #region 插入表体
            conn.Open();
            SqlCommand cmdD = conn.CreateCommand();
            cmdD.CommandType = CommandType.Text;

            cmdD.Parameters.Add("@FInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@FEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@FItemID", SqlDbType.Int);
            cmdD.Parameters.Add("@FBatchNo", SqlDbType.VarChar);
            cmdD.Parameters.Add("@FQty", SqlDbType.Decimal);

            cmdD.Parameters.Add("@FNote", SqlDbType.VarChar);
            cmdD.Parameters.Add("@FCostOBJID", SqlDbType.Int);
            cmdD.Parameters.Add("@FUnitID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCSPID", SqlDbType.Int);

            cmdD.Parameters.Add("@FSourceBillNo", SqlDbType.VarChar, 50);
            cmdD.Parameters.Add("@FSourceInterId", SqlDbType.Int);
            cmdD.Parameters.Add("@FPPBomEntryID", SqlDbType.Int);

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                try
                {
                    cmdD.Parameters["@FInterID"].Value = FInterID;
                    cmdD.Parameters["@FEntryID"].Value = i + 1;
                    cmdD.Parameters["@FItemID"].Value = dtDtl.Rows[i]["FItemID"].ToString();
                    cmdD.Parameters["@FBatchNo"].Value = dtDtl.Rows[i]["FBatchNo"].ToString();
                    cmdD.Parameters["@FQty"].Value = dtDtl.Rows[i]["FQty"].ToString();

                    cmdD.Parameters["@FNote"].Value = dtDtl.Rows[i]["FNote"].ToString();
                    cmdD.Parameters["@FCostOBJID"].Value = dtDtl.Rows[i]["FCostOBJID"].ToString();
                    cmdD.Parameters["@FUnitID"].Value = dtDtl.Rows[i]["FUnitID"].ToString();
                    cmdD.Parameters["@FSCStockID"].Value = dtDtl.Rows[i]["FSCStockID"].ToString();
                    cmdD.Parameters["@FDCSPID"].Value = dtDtl.Rows[i]["FDCSPID"].ToString();

                    cmdD.Parameters["@FSourceBillNo"].Value = dtDtl.Rows[i]["FSourceBillNo"].ToString();
                    cmdD.Parameters["@FSourceInterId"].Value = dtDtl.Rows[i]["FSourceInterId"].ToString();
                    cmdD.Parameters["@FPPBomEntryID"].Value = dtDtl.Rows[i]["FPPBomEntryID"].ToString();

                    strSQL = @"INSERT INTO dbo.ICStockBillEntry(FInterID,FEntryID,FBrNo,FItemID,FBatchNo,FQty,FQtyMust,FUnitID,FAuxQtyMust,Fauxqty, FSCStockID,FDCSPID,FSourceTranType,FSourceBillNo,FSourceInterId,FSourceEntryID,FICMOBillNo,FICMOInterID,FPPBomEntryID,FNote,FCostOBJID)
                    VALUES(@FInterID,@FEntryID,'0',@FItemID,@FBatchNo,@FQty,@FQty,@FUnitID,@FQty,@FQty, @FSCStockID,@FDCSPID,85,@FSourceBillNo,@FSourceInterId,1,@FSourceBillNo,@FSourceInterId,@FPPBomEntryID,@FNote,@FCostOBJID)";

                    cmdD.CommandText = strSQL;
                    cmdD.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    SqlOperation(0, "DELETE FROM ICStockBill WHERE FInterID = " + FInterID.ToString() + " DELETE FROM ICstockbillentry WHERE FInterID = " + FInterID.ToString());

                    conn.Close();
                    return "no@x004:" + ex.Message;
                }
            }
            #endregion

            #region 反写库存、生产订单和投料单
            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                if (dtDtl.Rows[i]["FDCSPID"] == null || dtDtl.Rows[i]["FDCSPID"].ToString() == "0")
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
                     SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FSCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FDCSPID"].ToString() + " FSPID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty - DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo); 
                    UPDATE ICMO SET FStockQty = FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FSourceInterId"].ToString() + @"; 
                    UPDATE PPBOMEntry SET FStockQty = FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FDetailID = " + dtDtl.Rows[i]["FDetailID"].ToString() + ";";
                else
                    strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
                     SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FSCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FDCSPID"].ToString() + " FSPID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty - DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo); 
                    UPDATE ICMO SET FStockQty = FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FSourceInterId"].ToString() + @"; 
                    UPDATE PPBOMEntry SET FStockQty = FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FDetailID = " + dtDtl.Rows[i]["FDetailID"].ToString() + ";";

                SqlOperation(0, strSQL);
            }
            #endregion

            //修改审核状态
            strSQL = "UPDATE ICStockBill SET FCheckDate = GETDATE(),FCheckerID = " + FFManagerID.ToString() + ",FStatus = 1 WHERE FINTERID = " + FInterID.ToString();
            SqlOperation(0, strSQL);

            //关闭conn
            if (conn.State == ConnectionState.Open)
                conn.Close();

            return "yes@ID:" + FInterID.ToString() + ";Number:" + FBillNo;
        }
        #endregion

        #region 调拨单
        /// <summary>
        /// 调拨单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FCheckerID</param>
        /// <param name="pDetails">表体：[FItemNumber|FSCStockNumber|FSCSPNumber|FBatchNo|FQty|FDCStockNumber|FDCSPNumber|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        public static string ICStockBillForTrans(string pHead, string pDetails)
        {
            object obj;
            string strSQL;
            DataTable dtDtl, dtCheck;
            DataRow dr;
            SqlConnection conn;

            int FInterID;
            string FBillNo;

            GetICMaxIDAndBillNo(41, out FInterID, out FBillNo);

            if (FBillNo.IndexOf("Error") >= 0)
                return "no@x001:" + FBillNo;

            //定义表头字段
            int FDeptID, FSManagerID, FFManagerID, FBillerID, FCheckerID;

            //定义表体字段
            int FItemID, FUnitID, FSCStockID, FSCSPID, FDCStockID, FDCSPID;
            decimal FQty;
            string FItem, FSCStock, FSCSP, FDCStock, FDCSP, FBatchNo = string.Empty, FNote = string.Empty;

            #region 字段赋值
            try
            {
                FDeptID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FDeptID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FSManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FFManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FFManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FBillerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FBillerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FCheckerID = int.Parse(pHead.Substring(pHead.IndexOf("|") + 1));//FCheckerID

                //
                dtDtl = new DataTable();
                dtDtl.Columns.Add("FItemID");
                dtDtl.Columns.Add("FUnitID");
                dtDtl.Columns.Add("FSCStockID");
                dtDtl.Columns.Add("FSCSPID");
                dtDtl.Columns.Add("FBatchNo");

                dtDtl.Columns.Add("FQty");
                dtDtl.Columns.Add("FDCStockID");
                dtDtl.Columns.Add("FDCSPID");
                dtDtl.Columns.Add("FNote");

                string strTemp;
                do
                {
                    if (pDetails.IndexOf("],[") > 0)
                    {
                        strTemp = pDetails.Substring(0, pDetails.IndexOf("]") + 1);
                        pDetails = pDetails.Substring(pDetails.IndexOf("]") + 2);
                    }
                    else
                    {
                        strTemp = pDetails;
                        pDetails = "";
                    }

                    FItem = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//FItemNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);

                    FSCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//FSCStockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FSCSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY

                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//FDCStockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FDCSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FNote = strTemp.Substring(0, strTemp.IndexOf("]"));//FNote

                    //仓库检验
                    if (FSCStock == "" || FDCStock == "")
                        return "no@请输入仓库编码。";

                    if (FSCStock != FDCStock)
                    {
                        obj = SqlOperation(3, "SELECT FItemID,FNumber FROM t_Stock WHERE FNumber IN('" + FSCStock + "','" + FDCStock + "')");

                        if (obj == null || ((DataTable)obj).Rows.Count < 2)
                            return "no@未找到仓库信息[" + FSCStock + "," + FDCStock + "]";

                        if (((DataTable)obj).Rows[0]["FNumber"].ToString() == FSCStock)
                        {
                            FSCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FSCStockID
                            FDCStockID = int.Parse(((DataTable)obj).Rows[1]["FItemID"].ToString());//FDCStockID
                        }
                        else
                        {
                            FSCStockID = int.Parse(((DataTable)obj).Rows[1]["FItemID"].ToString());//FSCStockID
                            FDCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FDCStockID
                        }
                    }
                    else
                    {
                        obj = SqlOperation(1, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FSCStock + "'");

                        if (obj == null)
                            return "no@未找到仓库信息[" + FSCStock + "]";

                        FSCStockID = int.Parse(obj.ToString());
                        FDCStockID = FSCStockID;
                    }

                    //仓位检验
                    if (FSCSP == "" || FDCSP == "")
                        return "no@请输入仓位编码。";

                    if (FSCSP != FDCSP)
                    {
                        obj = SqlOperation(3, "SELECT FSPID,FNumber FROM t_StockPlace WHERE FNumber IN('" + FSCSP + "','" + FDCSP + "')");

                        if (obj == null || ((DataTable)obj).Rows.Count < 2)
                            return "no@未找到仓位信息[" + FSCSP + "," + FDCSP + "]";

                        if (((DataTable)obj).Rows[0]["FNumber"].ToString() == FSCSP)
                        {
                            FSCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FSCSPID
                            FDCSPID = int.Parse(((DataTable)obj).Rows[1]["FSPID"].ToString());//FDCSPID
                        }
                        else
                        {
                            FSCSPID = int.Parse(((DataTable)obj).Rows[1]["FSPID"].ToString());//FSCSPID
                            FDCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FDCSPID
                        }
                    }
                    else
                    {
                        obj = SqlOperation(1, "SELECT FSPID FROM t_StockPlace WHERE FNumber = '" + FSCSP + "'");

                        if (obj == null)
                            return "no@未找到仓位信息[" + FSCSP + "]";

                        FSCSPID = int.Parse(obj.ToString());
                        FDCSPID = FSCSPID;
                    }

                    //物料检验
                    if (FItem == "")
                        return "no@请输入物料编码";

                    strSQL = "SELECT MTL.FItemID,MTL.FUnitID,ISNULL(INV.FQty,0) FStockQty,CASE WHEN " + FQty.ToString() + @" > ISNULL(INV.FQty,0) THEN -1 ELSE 0 END Flag
                    FROM t_ICItem MTL
                    LEFT JOIN ICInventory INV ON MTL.FItemID = INV.FItemID AND INV.FBatchNo = '" + FBatchNo + "' AND INV.FStockID = " + FSCStockID.ToString() + " AND INV.FStockPlaceID = " + FSCSPID.ToString() + @"
                    WHERE MTL.FNumber = '" + FItem + "'";

                    obj = SqlOperation(3, strSQL);

                    if (obj == null || ((DataTable)obj).Rows.Count == 0)
                        return "no@未找到物料信息[" + FItem + "]";

                    FItemID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());
                    FUnitID = int.Parse(((DataTable)obj).Rows[0]["FUnitID"].ToString());

                    if (int.Parse(((DataTable)obj).Rows[0]["Flag"].ToString()) == -1)
                    {
                        return "no@物料[" + FItem + "]调拨数量[" + FQty.ToString() + "]大于库存数量[" + decimal.Parse(((DataTable)obj).Rows[0]["FStockQty"].ToString()) + "]";
                    }

                    dr = dtDtl.NewRow();

                    dr["FItemID"] = FItemID;
                    dr["FUnitID"] = FUnitID;
                    dr["FSCStockID"] = FSCStockID;
                    dr["FSCSPID"] = FSCSPID;
                    dr["FBatchNo"] = FBatchNo;

                    dr["FQty"] = FQty;
                    dr["FDCStockID"] = FDCStockID;
                    dr["FDCSPID"] = FDCSPID;
                    dr["FNote"] = FNote;

                    dtDtl.Rows.Add(dr);
                }
                while (pDetails.Length > 0);
            }
            catch (Exception ex)
            {
                return "no@x002:" + ex.Message;
            }
            #endregion

            #region 判断 是否有本次调拨总数量大于即时库存数量
            dtCheck = dtDtl.Clone();//获取汇总FQty的物料信息
            dtCheck.ImportRow(dtDtl.Rows[0]);

            for (int i = 1; i < dtDtl.Rows.Count; i++)
            {
                for (int j = 0; j < dtCheck.Rows.Count; j++)
                {
                    if (dtCheck.Rows[j]["FItemID"] == dtDtl.Rows[i]["FItemID"] && dtCheck.Rows[j]["FBatchNo"] == dtDtl.Rows[i]["FBatchNo"] && dtCheck.Rows[j]["FSCStockID"] == dtDtl.Rows[i]["FSCStockID"] && dtCheck.Rows[j]["FSCSPID"] == dtDtl.Rows[i]["FSCSPID"])
                    {
                        dtCheck.Rows[j]["FQty"] = decimal.Parse(dtDtl.Rows[i]["FQty"].ToString()) + decimal.Parse(dtCheck.Rows[j]["FQty"].ToString());
                        break;
                    }

                    if (j == dtCheck.Rows.Count - 1)//未匹配到数据
                    {
                        dtCheck.ImportRow(dtDtl.Rows[i]);
                        break;
                    }
                }
            }
            //物料总调拨数量与库存数量对比
            for (int i = 0; i < dtCheck.Rows.Count; i++)
            {
                if (dtCheck.Rows[i]["FSCSPID"].ToString() != "0")
                    strSQL = @"SELECT MTL.FNumber,CASE WHEN " + dtCheck.Rows[i]["FQty"].ToString() + @" > ISNULL(INV.FQty,0) THEN ISNULL(INV.FQty,0) ELSE -1 END FQty
                    FROM t_ICItem MTL
                    LEFT JOIN ICInventory INV ON INV.FItemID = MTL.FItemID AND INV.FStockID = " + dtCheck.Rows[i]["FSCStockID"].ToString() + " AND INV.FBatchNo = '" + dtCheck.Rows[i]["FBatchNo"].ToString() + "' AND FStockPlaceID = " + dtCheck.Rows[i]["FSCSPID"].ToString() + @"
                    WHERE MTL.FItemID = " + dtCheck.Rows[i]["FItemID"].ToString();
                else
                    strSQL = @"SELECT MTL.FNumber,CASE WHEN " + dtCheck.Rows[i]["FQty"].ToString() + @" > ISNULL(INV.FQty,0) THEN ISNULL(INV.FQty,0) ELSE -1 END FQty
                    FROM t_ICItem MTL
                    LEFT JOIN ICInventory INV ON INV.FItemID = MTL.FItemID AND INV.FStockID = " + dtCheck.Rows[i]["FSCStockID"].ToString() + " AND INV.FBatchNo = '" + dtCheck.Rows[i]["FBatchNo"].ToString() + @"'
                    WHERE MTL.FItemID = " + dtCheck.Rows[i]["FItemID"].ToString();

                obj = SqlOperation(3, strSQL);

                if (obj == null || ((DataTable)obj).Rows.Count == 0)
                    return "no@物料输入有误";
                else if (decimal.Parse(((DataTable)obj).Rows[0]["FQty"].ToString()) != -1)
                    return "no@[" + ((DataTable)obj).Rows[0]["FNumber"].ToString() + "]物料本次调拨总数量[" + dtCheck.Rows[i]["FQty"].ToString() + "]大于库存数量[" + ((DataTable)obj).Rows[0]["FQty"].ToString() + "]";
            }
            #endregion

            conn = new SqlConnection(C_CONNECTIONSTRING);

            #region 插入表头
            try
            {
                conn.Open();
                SqlCommand cmdH = conn.CreateCommand();
                cmdH.CommandType = CommandType.Text;

                cmdH.Parameters.Add("@FInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillNo", SqlDbType.NVarChar, 255);

                cmdH.Parameters.Add("@FDeptID", SqlDbType.Int);
                cmdH.Parameters.Add("@FSManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FFManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FCheckerID", SqlDbType.Int);

                cmdH.Parameters["@FInterID"].Value = FInterID;
                cmdH.Parameters["@FBillNo"].Value = FBillNo;

                cmdH.Parameters["@FDeptID"].Value = FDeptID;
                cmdH.Parameters["@FSManagerID"].Value = FSManagerID;
                cmdH.Parameters["@FFManagerID"].Value = FFManagerID;
                cmdH.Parameters["@FBillerID"].Value = FBillerID;
                cmdH.Parameters["@FCheckerID"].Value = FCheckerID;

                strSQL = @"INSERT INTO dbo.ICStockBill(FInterID,FBillNo,FBrNo,FTranType,FROB,FDate,FDeptID,FPurposeID,FSManagerID,FFManagerID,  FBillerID,FCheckerID,FCheckDate,FStatus,FRefType,FMarketingStyle) 
                VALUES (@FInterID,@FBillNo,'0',41,1,CONVERT(VARCHAR(10),GETDATE(),120),@FDeptID,0,@FSManagerID,@FFManagerID,   @FBillerID,@FCheckerID,GETDATE(),1,12561,12530)";

                cmdH.CommandText = strSQL;
                cmdH.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                return "no@x003:" + ex.Message;
            }
            finally
            {
                conn.Close();
            }
            #endregion

            #region 插入表体
            conn.Open();
            SqlCommand cmdD = conn.CreateCommand();
            cmdD.CommandType = CommandType.Text;

            cmdD.Parameters.Add("@FInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@FEntryID", SqlDbType.Int);

            cmdD.Parameters.Add("@FItemID", SqlDbType.Int);
            cmdD.Parameters.Add("@FUnitID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSCSPID", SqlDbType.Int);
            cmdD.Parameters.Add("@FBatchNo", SqlDbType.VarChar);

            cmdD.Parameters.Add("@FQty", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FDCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FDCSPID", SqlDbType.Int);
            cmdD.Parameters.Add("@FNote", SqlDbType.VarChar);

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                try
                {
                    cmdD.Parameters["@FInterID"].Value = FInterID;
                    cmdD.Parameters["@FEntryID"].Value = i + 1;

                    cmdD.Parameters["@FItemID"].Value = dtDtl.Rows[i]["FItemID"].ToString();
                    cmdD.Parameters["@FUnitID"].Value = dtDtl.Rows[i]["FUnitID"].ToString();
                    cmdD.Parameters["@FSCStockID"].Value = dtDtl.Rows[i]["FSCStockID"].ToString();
                    cmdD.Parameters["@FSCSPID"].Value = dtDtl.Rows[i]["FSCSPID"].ToString();
                    cmdD.Parameters["@FBatchNo"].Value = dtDtl.Rows[i]["FBatchNo"].ToString();

                    cmdD.Parameters["@FQty"].Value = dtDtl.Rows[i]["FQty"].ToString();
                    cmdD.Parameters["@FDCStockID"].Value = dtDtl.Rows[i]["FDCStockID"].ToString();
                    cmdD.Parameters["@FDCSPID"].Value = dtDtl.Rows[i]["FDCSPID"].ToString();
                    cmdD.Parameters["@FNote"].Value = dtDtl.Rows[i]["FNote"].ToString();

                    strSQL = @"INSERT INTO dbo.ICstockbillentry(FInterID,FEntryID,FBrNo,FItemID,FBatchNo,FQty,FQtyMust,FUnitID,FAuxQtyMust,FAuxQty, FSCStockID,FSCSPID,FDCStockID,FDCSPID,FChkPassItem,FNote,FPlanMode)
                    VALUES(@FInterID,@FEntryID,'0',@FItemID,@FBatchNo,@FQty,@FQty,@FUnitID,@FQty,@FQty, @FSCStockID,@FSCSPID,@FDCStockID,@FDCSPID,1058,@FNote,14036)";

                    cmdD.CommandText = strSQL;
                    cmdD.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    //删除已生成的单据数据
                    SqlOperation(0, "DELETE FROM ICStockBill WHERE FInterID = " + FInterID.ToString() + " DELETE FROM ICstockbillentry WHERE FInterID = " + FInterID.ToString());

                    conn.Close();
                    return "no@x004:" + ex.Message;
                }
            }
            #endregion

            #region 反写库存
            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                if (dtDtl.Rows[i]["FDCStockID"] == dtDtl.Rows[i]["FSCStockID"] && dtDtl.Rows[i]["FDCSPID"] == dtDtl.Rows[i]["FSCSPID"])
                    continue;

                strSQL = @"
                --源仓扣库存
                UPDATE ICInventory SET FQty = FQty - " + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FItemID = " + dtDtl.Rows[i]["FItemID"].ToString() + " AND FBatchNo = '" + dtDtl.Rows[i]["FBatchNo"].ToString() + "' AND FStockID = " + dtDtl.Rows[i]["FSCStockID"].ToString() + " AND FStockPlaceID = " + dtDtl.Rows[i]["FSCSPID"].ToString() + @";
                --目标仓加库存
                MERGE INTO ICInventory AS IC
                USING
                (
                    SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + "' FBatchNo, " + dtDtl.Rows[i]["FDCStockID"].ToString() + " FStockID, " + dtDtl.Rows[i]["FDCSPID"].ToString() + @" FSPID
                ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo AND IC.FStockPlaceID = DT.FSPID
                WHEN MATCHED
                    THEN UPDATE SET FQty = IC.FQty + DT.FQty
                WHEN NOT MATCHED
                    THEN INSERT(FBrNo,FItemID,FStockID,FStockPlaceID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FSPID,DT.FQty,DT.FBatchNo);";

                SqlOperation(0, strSQL);
            }
            #endregion

            if (conn.State == ConnectionState.Open)
                conn.Close();

            return "yes@ID:" + FInterID.ToString() + ";Number:" + FBillNo;
        }
        #endregion

        #region 已经取消的方法

        #region 审核生产入库单 - 取消
        /// <summary>
        /// 审核入库单 - 审核单条分录单据(或调整成跟审核其他入库单一样有多条分录的单据)
        /// </summary>
        /// <param name="pAuditData">数据参数，格式：[FBillNo|FItem|FQty|FBatchNo|FDCStock|FCheckerID],[FBillNo|FItem|FQty|FBatchNo|FDCStock|FCheckerID]......</param>
        /// <returns>DataTable,结果集</returns>
        public static DataTable AuditInStock(string pAuditData)
        {
            int FItemID, FDCStockID, FCheckerID;
            decimal FQty;
            string FBillNo, FItem, FBatchNo, FDCStock;
            DataTable dt;
            DataRow dr;
            object obj;

            dt = new DataTable();

            dt.Columns.Add("入库单号");
            dt.Columns.Add("物料编码");
            dt.Columns.Add("数量");
            dt.Columns.Add("批号");
            dt.Columns.Add("仓库");

            dt.Columns.Add("审核结果");

            dt.TableName = "CheckResult";


            string strSQL, strTemp;
            do
            {
                if (pAuditData.IndexOf("],[") > 0)
                {
                    strTemp = pAuditData.Substring(0, pAuditData.IndexOf("]") + 1);
                    pAuditData = pAuditData.Substring(pAuditData.IndexOf("]") + 2);
                }
                else
                {
                    strTemp = pAuditData;
                    pAuditData = "";
                }

                dr = dt.NewRow();

                FBillNo = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//FBillNo
                strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                FItem = strTemp.Substring(0, strTemp.IndexOf("|"));//MTLNumber
                strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY
                strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                FDCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//StockNumber
                strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                FCheckerID = int.Parse(strTemp.Substring(0, strTemp.IndexOf("]")));//FCheckerID

                dr["入库单号"] = FBillNo;
                dr["物料编码"] = FItem;
                dr["数量"] = FQty;
                dr["批号"] = FBatchNo;
                dr["仓库"] = FDCStock;

                obj = SqlOperation(1, "SELECT FItemID FROM t_ICItem WHERE FNumber = '" + FItem + "'");
                if (obj == null)
                {
                    dr["审核结果"] = "审核失败:查询不到物料信息";
                    dt.Rows.Add(dr);

                    continue;
                }

                FItemID = int.Parse(obj.ToString());//MTLID

                obj = SqlOperation(1, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                if (obj == null)
                {
                    dr["审核结果"] = "审核失败:查询不到仓库信息";
                    dt.Rows.Add(dr);

                    continue;
                }

                FDCStockID = int.Parse(obj.ToString());//FDCStockID

                obj = SqlOperation(1, "SELECT COUNT(*) FROM ICStockBill A INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID WHERE A.FTranType = 2 AND A.FBillNo = '" + FBillNo + "' AND AE.FItemID = " + FItemID + " AND AE.FQty = " + FQty + " AND AE.FBatchNo = '" + FBatchNo + "' AND AE.FDCStockID = " + FDCStockID);

                //匹配信息
                if (obj.ToString().Equals("0"))
                {
                    dr["审核结果"] = "审核失败:信息不匹配，请检查单号,物料代码,数量,批次和仓库是否一致。";
                    dt.Rows.Add(dr);

                    continue;
                }

                //反写库存、审核生产入库单
                strSQL = @"MERGE INTO ICInventory AS IC
                USING
                (
	                SELECT " + FItemID + " FItemID, " + FDCStockID + " FStockID," + FQty + " FQty,'" + FBatchNo + @"' FBatchNo
                ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                WHEN MATCHED
                    THEN UPDATE SET FQty = IC.FQty + DT.FQty
                WHEN NOT MATCHED
                    THEN INSERT(FBrNo,FItemID,FStockID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty,DT.FBatchNo);
                UPDATE ICStockBill SET FCheckDate = GETDATE(),FCheckerID = " + FCheckerID + ",FStatus = 1 WHERE FBillNo = '" + FBillNo + "';";

                SqlOperation(0, strSQL);

                dr["审核结果"] = "审核成功";
                dt.Rows.Add(dr);
            }
            while (pAuditData.Length > 0);

            return dt;
        }
        #endregion

        #region 审核其他入库单 - 取消
        /// <summary>
        /// 审核其他入库单
        /// </summary>
        /// <param name="pAuditData">数据参数，格式：[FBillNo|FItem|FQty|FBatchNo|FDCStock|FCheckerID],[FBillNo|FItem|FQty|FBatchNo|FDCStock|FCheckerID]......</param>
        /// <returns>DataTable,结果集</returns>
        public static DataTable AuditQInStock(string pAuditData)
        {
            int FItemID, FDCStockID, FCheckerID;
            decimal FQty;
            string FBillNo, FItem, FBatchNo, FDCStock;
            DataTable dt;
            DataRow dr;
            object obj;

            dt = new DataTable();

            dt.Columns.Add("其他入库单号");
            dt.Columns.Add("物料编码");
            dt.Columns.Add("数量");
            dt.Columns.Add("批号");
            dt.Columns.Add("仓库");

            dt.Columns.Add("审核结果");
            dt.Columns.Add("FItemID");
            dt.Columns.Add("FDCStockID");
            dt.Columns.Add("FCheckerID");

            dt.TableName = "AuditResult";

            string strSQL, strTemp;
            int iPrevious = 0;//订单初始序号
            bool bCheck = true;//审核状态

            List<string> lstDate = new List<string>();
            do
            {
                if (pAuditData.IndexOf("],[") > 0)
                {
                    lstDate.Add(pAuditData.Substring(0, pAuditData.IndexOf("]") + 1));
                    pAuditData = pAuditData.Substring(pAuditData.IndexOf("]") + 2);
                }
                else
                {
                    lstDate.Add(pAuditData);
                    pAuditData = string.Empty;
                }
            } while (pAuditData.Length > 0);

            for (int i = 0; i < lstDate.Count; i++)
            {
                if (i == 0)
                {
                    strTemp = lstDate[0];
                    FBillNo = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//FBillNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FItem = strTemp.Substring(0, strTemp.IndexOf("|"));//MTLNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//StockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FCheckerID = int.Parse(strTemp.Substring(0, strTemp.IndexOf("]")));//FCheckerID

                    dr = dt.NewRow();
                    dr["其他入库单号"] = FBillNo;
                    dr["物料编码"] = FItem;
                    dr["数量"] = FQty;
                    dr["批号"] = FBatchNo;
                    dr["仓库"] = FDCStock;

                    obj = SqlOperation(1, "SELECT FStatus FROM ICStockBill WHERE FBillNo = '" + FBillNo + "'");
                    if (obj == null)
                    {
                        dr["审核结果"] = "审核失败:其他入库单号不存在";
                        dt.Rows.Add(dr);
                        bCheck = false;
                        continue;
                    }
                    else if (obj.ToString() == "1")
                    {
                        dr["审核结果"] = "审核失败:其他入库单号已经审核";
                        dt.Rows.Add(dr);
                        bCheck = false;
                        continue;
                    }


                    obj = SqlOperation(1, "SELECT FItemID FROM t_ICItem WHERE FNumber = '" + FItem + "'");
                    if (obj == null)
                    {
                        dr["审核结果"] = "审核失败:查询不到物料信息";
                        dt.Rows.Add(dr);
                        bCheck = false;
                        continue;
                    }

                    FItemID = int.Parse(obj.ToString());//MTLID

                    obj = SqlOperation(1, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                    if (obj == null)
                    {
                        dr["审核结果"] = "审核失败:查询不到仓库信息";
                        dt.Rows.Add(dr);
                        bCheck = false;
                        continue;
                    }

                    FDCStockID = int.Parse(obj.ToString());//FDCStockID

                    obj = SqlOperation(1, "SELECT COUNT(*) FROM ICStockBill A INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID WHERE A.FTranType = 10 AND A.FBillNo = '" + FBillNo + "' AND AE.FItemID = " + FItemID + " AND AE.FQty = " + FQty + " AND AE.FBatchNo = '" + FBatchNo + "' AND AE.FDCStockID = " + FDCStockID);

                    //匹配信息
                    if (obj.ToString().Equals("0"))
                    {
                        dr["审核结果"] = "审核失败:信息不匹配，请检查单号,物料代码,数量,批次和仓库是否一致。";
                        dt.Rows.Add(dr);
                        bCheck = false;
                        continue;
                    }
                    dr["审核结果"] = "";
                    dr["FItemID"] = FItemID;
                    dr["FDCStockID"] = FDCStockID;
                    dr["FCheckerID"] = FCheckerID;
                    dt.Rows.Add(dr);

                    //当只有一条信息的时候
                    if (lstDate.Count == 1)
                    {
                        //检查分录数量是否只有一条

                        //反写库存，修改审核状态、审核人、审核时间
                        strSQL = @"MERGE INTO ICInventory AS IC
                        USING
                        (
                         SELECT " + FItemID + " FItemID," + FQty + " FQty,'" + FBatchNo + "' FBatchNo, " + FDCStockID + @" FStockID
                        ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                        WHEN MATCHED
                            THEN UPDATE SET FQty = IC.FQty + DT.FQty
                        WHEN NOT MATCHED
                            THEN INSERT(FBrNo,FItemID,FStockID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty,DT.FBatchNo);
                        UPDATE ICStockBill SET FCheckDate = GETDATE(),FCheckerID = " + FCheckerID + ",FStatus = 1 WHERE FBillNo = '" + FBillNo + "';";
                        SqlOperation(0, strSQL);

                        //修改审核结果
                        dt.Rows[0]["审核结果"] = "审核成功";
                    }
                }
                else
                {
                    strTemp = lstDate[i];
                    FBillNo = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//FBillNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FItem = strTemp.Substring(0, strTemp.IndexOf("|"));//MTLNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//StockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FCheckerID = int.Parse(strTemp.Substring(0, strTemp.IndexOf("]")));//FCheckerID

                    if (dt.Rows[i - 1]["其他入库单号"].ToString() == FBillNo)//跟上一行单号相同
                    {
                        dr = dt.NewRow();
                        dr["其他入库单号"] = FBillNo;
                        dr["物料编码"] = FItem;
                        dr["数量"] = FQty;
                        dr["批号"] = FBatchNo;
                        dr["仓库"] = FDCStock;

                        if (!bCheck)//同一订单内，前面分录已经审核失败
                        {
                            dr["审核结果"] = "审核失败:存在审核失败分录";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        obj = SqlOperation(1, "SELECT FItemID FROM t_ICItem WHERE FNumber = '" + FItem + "'");
                        if (obj == null)
                        {
                            dr["审核结果"] = "审核失败:查询不到物料信息";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        FItemID = int.Parse(obj.ToString());//MTLID

                        obj = SqlOperation(1, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                        if (obj == null)
                        {
                            dr["审核结果"] = "审核失败:查询不到仓库信息";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        FDCStockID = int.Parse(obj.ToString());//FDCStockID

                        obj = SqlOperation(1, "SELECT COUNT(*) FROM ICStockBill A INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID WHERE A.FTranType = 10 AND A.FBillNo = '" + FBillNo + "' AND AE.FItemID = " + FItemID + " AND AE.FQty = " + FQty + " AND AE.FBatchNo = '" + FBatchNo + "' AND AE.FDCStockID = " + FDCStockID);

                        //匹配信息
                        if (obj.ToString().Equals("0"))
                        {
                            dr["审核结果"] = "审核失败:信息不匹配，请检查单号,物料代码,数量,批次和仓库是否一致。";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }
                        dr["审核结果"] = "";
                        dr["FItemID"] = FItemID;
                        dr["FDCStockID"] = FDCStockID;
                        dr["FCheckerID"] = FCheckerID;
                        dt.Rows.Add(dr);

                        //当前为最后一条信息的时候
                        if (lstDate.Count == i + 1)
                        {
                            //检查分录数量是否跟传递的分录数量一致

                            //
                            for (int j = iPrevious; j < lstDate.Count; j++)
                            {
                                //反写库存
                                strSQL = @"MERGE INTO ICInventory AS IC
                                USING
                                (
                                 SELECT " + dt.Rows[j]["FItemID"].ToString() + " FItemID," + dt.Rows[j]["数量"].ToString() + " FQty,'" + dt.Rows[j]["批号"].ToString() + "' FBatchNo, " + dt.Rows[j]["FDCStockID"].ToString() + @" FStockID
                                ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                                WHEN MATCHED
                                    THEN UPDATE SET FQty = IC.FQty + DT.FQty
                                WHEN NOT MATCHED
                                    THEN INSERT(FBrNo,FItemID,FStockID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty,DT.FBatchNo);";
                                SqlOperation(0, strSQL);

                                //修改审核结果
                                dt.Rows[j]["审核结果"] = "审核成功";
                            }
                            //反写审核状态、审核人、审核时间
                            strSQL = "UPDATE ICStockBill SET FCheckDate = GETDATE(),FCheckerID = " + FCheckerID + ",FStatus = 1 WHERE FBillNo = '" + FBillNo + "'";
                            SqlOperation(0, strSQL);
                        }
                    }
                    else//不同单号
                    {
                        //检查之前的单号 分录数量是否跟当前传递的分录数量一致

                        //处理之前的单号
                        for (int j = iPrevious; j < i; j++)
                        {
                            //反写库存
                            strSQL = @"MERGE INTO ICInventory AS IC
                            USING
                            (
                                SELECT " + dt.Rows[j]["FItemID"].ToString() + " FItemID," + dt.Rows[j]["数量"].ToString() + " FQty,'" + dt.Rows[j]["批号"].ToString() + "' FBatchNo, " + dt.Rows[j]["FDCStockID"].ToString() + @" FStockID
                            ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                            WHEN MATCHED
                                THEN UPDATE SET FQty = IC.FQty + DT.FQty
                            WHEN NOT MATCHED
                                THEN INSERT(FBrNo,FItemID,FStockID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty,DT.FBatchNo);";
                            SqlOperation(0, strSQL);

                            //修改审核结果
                            dt.Rows[j]["审核结果"] = "审核成功";
                        }
                        //反写审核状态、审核人、审核时间
                        strSQL = "UPDATE ICStockBill SET FCheckDate = GETDATE(),FCheckerID = " + dt.Rows[iPrevious]["FCheckerID"].ToString() + ",FStatus = 1 WHERE FBillNo = '" + dt.Rows[iPrevious]["其他入库单号"].ToString() + "'";
                        SqlOperation(0, strSQL);

                        iPrevious = i;//刷新iPrevious值
                        bCheck = true;//刷新bCheck值

                        dr = dt.NewRow();
                        dr["其他入库单号"] = FBillNo;
                        dr["物料编码"] = FItem;
                        dr["数量"] = FQty;
                        dr["批号"] = FBatchNo;
                        dr["仓库"] = FDCStock;

                        obj = SqlOperation(1, "SELECT FStatus FROM ICStockBill WHERE FBillNo = '" + FBillNo + "'");
                        if (obj == null)
                        {
                            dr["审核结果"] = "审核失败:其他入库单号不存在";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }
                        else if (obj.ToString() == "1")
                        {
                            dr["审核结果"] = "审核失败:其他入库单号已经审核";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        obj = SqlOperation(1, "SELECT FItemID FROM t_ICItem WHERE FNumber = '" + FItem + "'");
                        if (obj == null)
                        {
                            dr["审核结果"] = "审核失败:查询不到物料信息";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        FItemID = int.Parse(obj.ToString());//MTLID

                        obj = SqlOperation(1, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                        if (obj == null)
                        {
                            dr["审核结果"] = "审核失败:查询不到仓库信息";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        FDCStockID = int.Parse(obj.ToString());//FDCStockID

                        obj = SqlOperation(1, "SELECT COUNT(*) FROM ICStockBill A INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID WHERE A.FTranType = 10 AND A.FBillNo = '" + FBillNo + "' AND AE.FItemID = " + FItemID + " AND AE.FQty = " + FQty + " AND AE.FBatchNo = '" + FBatchNo + "' AND AE.FDCStockID = " + FDCStockID);

                        //匹配信息
                        if (obj.ToString().Equals("0"))
                        {
                            dr["审核结果"] = "审核失败:信息不匹配，请检查单号,物料代码,数量,批次和仓库是否一致。";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }
                        dr["审核结果"] = "";
                        dr["FItemID"] = FItemID;
                        dr["FDCStockID"] = FDCStockID;
                        dt.Rows.Add(dr);

                        //当前为最后一条信息的时候
                        if (lstDate.Count == i + 1)
                        {
                            //检查分录数量是否跟当前传递的分录数量一致

                            //反写库存，修改审核状态、审核人、审核时间
                            strSQL = @"MERGE INTO ICInventory AS IC
                            USING
                            (
                             SELECT " + FItemID + " FItemID," + FQty + " FQty,'" + FBatchNo + "' FBatchNo, " + FDCStockID + @" FStockID
                            ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                            WHEN MATCHED
                                THEN UPDATE SET FQty = IC.FQty + DT.FQty
                            WHEN NOT MATCHED
                                THEN INSERT(FBrNo,FItemID,FStockID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty,DT.FBatchNo);
                            UPDATE ICStockBill SET FCheckDate = GETDATE(),FCheckerID = " + FCheckerID + ",FStatus = 1 WHERE FBillNo = '" + FBillNo + "';";
                            SqlOperation(0, strSQL);

                            //修改审核结果
                            dt.Rows[0]["审核结果"] = "审核成功";
                        }
                    }
                }
            }
            return dt;
        }
        #endregion

        #region 审核调拨单 - 取消
        /// <summary>
        /// 审核调拨单
        /// </summary>
        /// <param name="pAuditData">数据参数，格式：[FBillNo|FItem|FQty|FBatchNo|FDCStock|FCheckerID],[FBillNo|FItem|FQty|FBatchNo|FDCStock|FCheckerID]......</param>
        /// <returns>DataTable,结果集</returns>
        public static DataTable AuditTrans(string pAuditData)
        {
            int FItemID, FDCStockID, FCheckerID;
            decimal FQty;
            string FBillNo, FItem, FBatchNo, FDCStock;
            DataTable dt;
            DataRow dr;
            object obj;

            dt = new DataTable();

            dt.Columns.Add("调拨单号");
            dt.Columns.Add("物料编码");
            dt.Columns.Add("数量");
            dt.Columns.Add("批号");
            dt.Columns.Add("仓库");

            dt.Columns.Add("审核结果");
            dt.Columns.Add("FItemID");
            dt.Columns.Add("FDCStockID");
            dt.Columns.Add("FCheckerID");

            dt.TableName = "AuditResult";

            string strSQL, strTemp;
            int iPrevious = 0;//订单初始序号
            bool bCheck = true;//审核状态

            List<string> lstDate = new List<string>();
            do
            {
                if (pAuditData.IndexOf("],[") > 0)
                {
                    lstDate.Add(pAuditData.Substring(0, pAuditData.IndexOf("]") + 1));
                    pAuditData = pAuditData.Substring(pAuditData.IndexOf("]") + 2);
                }
                else
                {
                    lstDate.Add(pAuditData);
                    pAuditData = string.Empty;
                }
            } while (pAuditData.Length > 0);

            for (int i = 0; i < lstDate.Count; i++)
            {
                if (i == 0)
                {
                    strTemp = lstDate[0];
                    FBillNo = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//FBillNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FItem = strTemp.Substring(0, strTemp.IndexOf("|"));//MTLNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//StockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FCheckerID = int.Parse(strTemp.Substring(0, strTemp.IndexOf("]")));//FCheckerID

                    dr = dt.NewRow();
                    dr["调拨单号"] = FBillNo;
                    dr["物料编码"] = FItem;
                    dr["数量"] = FQty;
                    dr["批号"] = FBatchNo;
                    dr["仓库"] = FDCStock;

                    obj = SqlOperation(1, "SELECT FStatus FROM ICStockBill WHERE FBillNo = '" + FBillNo + "'");
                    if (obj == null)
                    {
                        dr["审核结果"] = "审核失败:调拨单号不存在";
                        dt.Rows.Add(dr);
                        bCheck = false;
                        continue;
                    }
                    else if (obj.ToString() == "1")
                    {
                        dr["审核结果"] = "审核失败:调拨单号已经审核";
                        dt.Rows.Add(dr);
                        bCheck = false;
                        continue;
                    }


                    obj = SqlOperation(1, "SELECT FItemID FROM t_ICItem WHERE FNumber = '" + FItem + "'");
                    if (obj == null)
                    {
                        dr["审核结果"] = "审核失败:查询不到物料信息";
                        dt.Rows.Add(dr);
                        bCheck = false;
                        continue;
                    }

                    FItemID = int.Parse(obj.ToString());//MTLID

                    obj = SqlOperation(1, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                    if (obj == null)
                    {
                        dr["审核结果"] = "审核失败:查询不到仓库信息";
                        dt.Rows.Add(dr);
                        bCheck = false;
                        continue;
                    }

                    FDCStockID = int.Parse(obj.ToString());//FDCStockID

                    obj = SqlOperation(1, "SELECT COUNT(*) FROM ICStockBill A INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID WHERE A.FBillNo = '" + FBillNo + "' AND AE.FItemID = " + FItemID + " AND AE.FQty = " + FQty + " AND AE.FBatchNo = '" + FBatchNo + "' AND AE.FDCStockID = " + FDCStockID);

                    //匹配信息
                    if (obj.ToString().Equals("0"))
                    {
                        dr["审核结果"] = "审核失败:信息不匹配，请检查单号,物料代码,数量,批次和仓库是否一致。";
                        dt.Rows.Add(dr);
                        bCheck = false;
                        continue;
                    }
                    dr["审核结果"] = "";
                    dr["FItemID"] = FItemID;
                    dr["FDCStockID"] = FDCStockID;
                    dr["FCheckerID"] = FCheckerID;
                    dt.Rows.Add(dr);

                    //当只有一条信息的时候
                    if (lstDate.Count == 1)
                    {
                        //检查分录数量是否只有一条

                        //反写库存，修改审核状态、审核人、审核时间
                        strSQL = @"--目标仓加库存
                        MERGE INTO ICInventory AS IC
                        USING
                        (
                         SELECT " + FItemID + " FItemID," + FQty + " FQty,'" + FBatchNo + "' FBatchNo, " + FDCStockID + @" FStockID
                        ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID --AND IC.FBatchNo = DT.FBatchNo
                        WHEN MATCHED
                            THEN UPDATE SET FQty = IC.FQty + DT.FQty, FBatchNo = DT.FBatchNo
                        WHEN NOT MATCHED
                            THEN INSERT(FBrNo,FItemID,FStockID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty,DT.FBatchNo);
                        UPDATE ICStockBill SET FCheckDate = GETDATE(),FCheckerID = " + FCheckerID + ",FStatus = 1 WHERE FBillNo = '" + FBillNo + @"';
                        --源仓扣库存
                        ";

                        SqlOperation(0, strSQL);

                        //修改审核结果
                        dt.Rows[0]["审核结果"] = "审核成功";
                    }
                }
                else
                {
                    strTemp = lstDate[i];
                    FBillNo = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//FBillNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FItem = strTemp.Substring(0, strTemp.IndexOf("|"));//MTLNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FDCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//StockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FCheckerID = int.Parse(strTemp.Substring(0, strTemp.IndexOf("]")));//FCheckerID

                    if (dt.Rows[i - 1]["调拨单号"].ToString() == FBillNo)//跟上一行单号相同
                    {
                        dr = dt.NewRow();
                        dr["调拨单号"] = FBillNo;
                        dr["物料编码"] = FItem;
                        dr["数量"] = FQty;
                        dr["批号"] = FBatchNo;
                        dr["仓库"] = FDCStock;

                        if (!bCheck)//同一订单内，前面分录已经审核失败
                        {
                            dr["审核结果"] = "审核失败";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        obj = SqlOperation(1, "SELECT FItemID FROM t_ICItem WHERE FNumber = '" + FItem + "'");
                        if (obj == null)
                        {
                            dr["审核结果"] = "审核失败:查询不到物料信息";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        FItemID = int.Parse(obj.ToString());//MTLID

                        obj = SqlOperation(1, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                        if (obj == null)
                        {
                            dr["审核结果"] = "审核失败:查询不到仓库信息";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        FDCStockID = int.Parse(obj.ToString());//FDCStockID

                        obj = SqlOperation(1, "SELECT COUNT(*) FROM ICStockBill A INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID WHERE A.FBillNo = '" + FBillNo + "' AND AE.FItemID = " + FItemID + " AND AE.FQty = " + FQty + " AND AE.FBatchNo = '" + FBatchNo + "' AND AE.FDCStockID = " + FDCStockID);

                        //匹配信息
                        if (obj.ToString().Equals("0"))
                        {
                            dr["审核结果"] = "审核失败:信息不匹配，请检查单号,物料代码,数量,批次和仓库是否一致。";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }
                        dr["审核结果"] = "";
                        dr["FItemID"] = FItemID;
                        dr["FDCStockID"] = FDCStockID;
                        dr["FCheckerID"] = FCheckerID;
                        dt.Rows.Add(dr);

                        //当前为最后一条信息的时候
                        if (lstDate.Count == i + 1)
                        {
                            //检查分录数量是否跟传递的分录数量一致

                            //
                            for (int j = iPrevious; j < lstDate.Count; j++)
                            {
                                //反写库存
                                strSQL = @"MERGE INTO ICInventory AS IC
                                USING
                                (
                                 SELECT " + dt.Rows[j]["FItemID"].ToString() + " FItemID," + dt.Rows[j]["数量"].ToString() + " FQty,'" + dt.Rows[j]["批号"].ToString() + "' FBatchNo, " + dt.Rows[j]["FDCStockID"].ToString() + @" FStockID
                                ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID --AND IC.FBatchNo = DT.FBatchNo
                                WHEN MATCHED
                                    THEN UPDATE SET FQty = IC.FQty + DT.FQty, FBatchNo = DT.FBatchNo
                                WHEN NOT MATCHED
                                    THEN INSERT(FBrNo,FItemID,FStockID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty,DT.FBatchNo);";
                                SqlOperation(0, strSQL);

                                //修改审核结果
                                dt.Rows[j]["审核结果"] = "审核成功";
                            }
                            //反写审核状态、审核人、审核时间
                            strSQL = "UPDATE ICStockBill SET FCheckDate = GETDATE(),FCheckerID = " + FCheckerID + ",FStatus = 1 WHERE FBillNo = '" + FBillNo + "'";
                            SqlOperation(0, strSQL);
                        }
                    }
                    else//不同单号
                    {
                        //检查之前的单号 分录数量是否跟当前传递的分录数量一致

                        //处理之前的单号
                        for (int j = iPrevious; j < i; j++)
                        {
                            //反写库存
                            strSQL = @"MERGE INTO ICInventory AS IC
                                USING
                                (
                                 SELECT " + dt.Rows[j]["FItemID"].ToString() + " FItemID," + dt.Rows[j]["数量"].ToString() + " FQty,'" + dt.Rows[j]["批号"].ToString() + "' FBatchNo, " + dt.Rows[j]["FDCStockID"].ToString() + @" FStockID
                                ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID --AND IC.FBatchNo = DT.FBatchNo
                                WHEN MATCHED
                                    THEN UPDATE SET FQty = IC.FQty + DT.FQty, FBatchNo = DT.FBatchNo
                                WHEN NOT MATCHED
                                    THEN INSERT(FBrNo,FItemID,FStockID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty,DT.FBatchNo);";
                            SqlOperation(0, strSQL);

                            //修改审核结果
                            dt.Rows[j]["审核结果"] = "审核成功";
                        }
                        //反写审核状态、审核人、审核时间
                        strSQL = "UPDATE ICStockBill SET FCheckDate = GETDATE(),FCheckerID = " + dt.Rows[iPrevious]["FCheckerID"].ToString() + ",FStatus = 1 WHERE FBillNo = '" + dt.Rows[iPrevious]["调拨单号"].ToString() + "'";
                        SqlOperation(0, strSQL);

                        iPrevious = i;//刷新iPrevious值
                        bCheck = true;//刷新bCheck值

                        dr = dt.NewRow();
                        dr["调拨单号"] = FBillNo;
                        dr["物料编码"] = FItem;
                        dr["数量"] = FQty;
                        dr["批号"] = FBatchNo;
                        dr["仓库"] = FDCStock;

                        obj = SqlOperation(1, "SELECT FStatus FROM ICStockBill WHERE FBillNo = '" + FBillNo + "'");
                        if (obj == null)
                        {
                            dr["审核结果"] = "审核失败:调拨单号不存在";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }
                        else if (obj.ToString() == "1")
                        {
                            dr["审核结果"] = "审核失败:调拨单号已经审核";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        obj = SqlOperation(1, "SELECT FItemID FROM t_ICItem WHERE FNumber = '" + FItem + "'");
                        if (obj == null)
                        {
                            dr["审核结果"] = "审核失败:查询不到物料信息";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        FItemID = int.Parse(obj.ToString());//MTLID

                        obj = SqlOperation(1, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FDCStock + "'");
                        if (obj == null)
                        {
                            dr["审核结果"] = "审核失败:查询不到仓库信息";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }

                        FDCStockID = int.Parse(obj.ToString());//FDCStockID

                        obj = SqlOperation(1, "SELECT COUNT(*) FROM ICStockBill A INNER JOIN ICStockBillEntry AE ON A.FInterID = AE.FInterID WHERE A.FBillNo = '" + FBillNo + "' AND AE.FItemID = " + FItemID + " AND AE.FQty = " + FQty + " AND AE.FBatchNo = '" + FBatchNo + "' AND AE.FDCStockID = " + FDCStockID);

                        //匹配信息
                        if (obj.ToString().Equals("0"))
                        {
                            dr["审核结果"] = "审核失败:信息不匹配，请检查单号,物料代码,数量,批次和仓库是否一致。";
                            dt.Rows.Add(dr);
                            bCheck = false;
                            continue;
                        }
                        dr["审核结果"] = "";
                        dr["FItemID"] = FItemID;
                        dr["FDCStockID"] = FDCStockID;
                        dt.Rows.Add(dr);

                        //当前为最后一条信息的时候
                        if (lstDate.Count == i + 1)
                        {
                            //检查分录数量是否跟当前传递的分录数量一致

                            //反写库存，修改审核状态、审核人、审核时间
                            strSQL = @"MERGE INTO ICInventory AS IC
                            USING
                            (
                             SELECT " + FItemID + " FItemID," + FQty + " FQty,'" + FBatchNo + "' FBatchNo, " + FDCStockID + @" FStockID
                            ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID --AND IC.FBatchNo = DT.FBatchNo
                            WHEN MATCHED
                                THEN UPDATE SET FQty = IC.FQty + DT.FQty, FBatchNo = DT.FBatchNo
                            WHEN NOT MATCHED
                                THEN INSERT(FBrNo,FItemID,FStockID,FQty,FBatchNo) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty,DT.FBatchNo);
                            UPDATE ICStockBill SET FCheckDate = GETDATE(),FCheckerID = " + FCheckerID + ",FStatus = 1 WHERE FBillNo = '" + FBillNo + "';";
                            SqlOperation(0, strSQL);

                            //修改审核结果
                            dt.Rows[0]["审核结果"] = "审核成功";
                        }
                    }
                }
            }
            return dt;
        }
        #endregion

        #region 其他出库单 - 取消
        /// <summary>
        /// 其他出库单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FNote</param>
        /// <param name="pDetails">表体：[FItemNumber|FDCStockNumber|FDCSPNumber|FBatchNo|FQty|FQty|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        public static string ICStockBillForQOut(string pHead, string pDetails)
        {
            object obj;
            string strSQL;
            DataTable dtDtl;
            DataRow dr;

            int FInterID;
            string FBillNo;

            SqlConnection conn = new SqlConnection(C_CONNECTIONSTRING);
            try
            {
                conn.Open();

                //内码
                SqlCommand cmd = new SqlCommand("GetICMaxNum", conn);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@TableName", SqlDbType.VarChar, 50);
                cmd.Parameters.Add("@FInterID", SqlDbType.Int);
                cmd.Parameters.Add("@Increment", SqlDbType.Int);
                cmd.Parameters.Add("@UserID", SqlDbType.Int);

                cmd.Parameters["@TableName"].Value = "ICStockBill";
                cmd.Parameters["@FInterID"].Direction = ParameterDirection.Output;
                cmd.Parameters["@Increment"].Value = 1;
                cmd.Parameters["@UserID"].Value = 16394;

                cmd.ExecuteNonQuery();

                FInterID = int.Parse(cmd.Parameters["@FInterID"].Value.ToString());

                //编号
                SqlCommand cmd2 = new SqlCommand("GetICBillNo", conn);

                cmd2.CommandType = CommandType.StoredProcedure;
                cmd2.Parameters.Add("@IsSave", SqlDbType.SmallInt);
                cmd2.Parameters.Add("@FBillType", SqlDbType.Int);
                cmd2.Parameters.Add("@BillID", SqlDbType.VarChar, 50);

                cmd2.Parameters["@IsSave"].Value = 1;
                cmd2.Parameters["@FBillType"].Value = 29;
                cmd2.Parameters["@BillID"].Direction = ParameterDirection.Output;

                cmd2.ExecuteNonQuery();

                FBillNo = cmd2.Parameters["@BillID"].Value.ToString();
            }
            catch (Exception ex)
            {
                return "no@" + ex.Message;
            }
            finally
            {
                conn.Close();
            }

            //定义表头字段
            string FNote;
            int FDeptID, FSManagerID, FFManagerID, FBillerID;

            //定义表体字段
            int FItemID, FUnitID, FSCStockID, FSCSPID;
            decimal FPrice, FQty;
            string FNoteD, FItem, FSCStock, FSCSP, FBatchNo = string.Empty;

            //物料
            bool FBatchManager = false;

            //字段赋值
            try
            {
                //解释表头字段
                FDeptID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FDeptID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FSManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FFManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FFManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FBillerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FBillerID
                //
                FNote = pHead.Substring(pHead.IndexOf("|") + 1);//FNote

                //解释表体字段
                dtDtl = new DataTable();
                dtDtl.Columns.Add("FItemID");
                dtDtl.Columns.Add("FUnitID");
                dtDtl.Columns.Add("FSCStockID");
                dtDtl.Columns.Add("FSCSPID");
                dtDtl.Columns.Add("FBatchNo");

                dtDtl.Columns.Add("FQty");
                dtDtl.Columns.Add("FPrice");
                dtDtl.Columns.Add("FAmount");
                dtDtl.Columns.Add("FNote");

                string strTemp;
                do
                {
                    if (pDetails.IndexOf("],[") > 0)
                    {
                        strTemp = pDetails.Substring(0, pDetails.IndexOf("]") + 1);
                        pDetails = pDetails.Substring(pDetails.IndexOf("]") + 2);
                    }
                    else
                    {
                        strTemp = pDetails;
                        pDetails = "";
                    }

                    FItem = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//MTLNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//FStockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY

                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FPrice = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FPrice
                    //
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FNoteD = strTemp.Substring(0, strTemp.IndexOf("]"));//FNoteD

                    obj = SqlOperation(3, "SELECT FItemID,FUnitID,FBatchManager FROM t_ICItem WHERE FNumber = '" + FItem + "'");
                    if (obj == null || ((DataTable)obj).Rows.Count == 0)
                        return "no@未找到对应的物料信息[" + FItem + "]";

                    FItemID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//MTLID
                    FUnitID = int.Parse(((DataTable)obj).Rows[0]["FUnitID"].ToString());//UnitID

                    FBatchManager = ((DataTable)obj).Rows[0]["FBatchManager"].ToString() == "0" ? false : true;//是否采用业务批次管理

                    if (FSCStock == "")
                        FSCStockID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FSCStock + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓库信息[" + FSCStock + "]";
                        FSCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FSCStockID
                    }

                    if (FSCSP == "")
                        FSCSPID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FSPID FROM t_StockPlace WHERE FNumber = '" + FSCSP + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓位信息[" + FSCSP + "]";
                        FSCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FSCSPID
                    }

                    if (string.IsNullOrEmpty(FBatchNo) && FBatchManager)
                    {
                        return "no@[" + FItem + "]物料已经启用批次号管理，请携带批次号。";
                    }

                    dr = dtDtl.NewRow();

                    dr["FItemID"] = FItemID;
                    dr["FUnitID"] = FUnitID;
                    dr["FSCStockID"] = FSCStockID;
                    dr["FSCSPID"] = FSCSPID;
                    dr["FBatchNo"] = FBatchNo;

                    dr["FQty"] = FQty;
                    dr["FPrice"] = FPrice;
                    dr["FAmount"] = FQty * FPrice;
                    dr["FNote"] = FNoteD;

                    dtDtl.Rows.Add(dr);
                }
                while (pDetails.Length > 0);
            }
            catch (Exception ex)
            {
                return "no@" + ex.Message;
            }

            //插入主表
            try
            {
                conn.Open();
                SqlCommand cmdH = conn.CreateCommand();
                cmdH.CommandType = CommandType.Text;

                cmdH.Parameters.Add("@FInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillNo", SqlDbType.VarChar, 255);
                cmdH.Parameters.Add("@FNote", SqlDbType.VarChar, 255);
                cmdH.Parameters.Add("@FDeptID", SqlDbType.Int);

                cmdH.Parameters.Add("@FSManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FFManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillerID", SqlDbType.Int);

                cmdH.Parameters["@FInterID"].Value = FInterID;
                cmdH.Parameters["@FBillNo"].Value = FBillNo;
                cmdH.Parameters["@FNote"].Value = FNote;
                cmdH.Parameters["@FDeptID"].Value = FDeptID;

                cmdH.Parameters["@FSManagerID"].Value = FSManagerID;
                cmdH.Parameters["@FFManagerID"].Value = FFManagerID;
                cmdH.Parameters["@FBillerID"].Value = FBillerID;

                strSQL = @"INSERT INTO dbo.ICStockBill(FInterID,FBillNo,FBrNo,FTranType,FROB,   Fdate,FNote,FDeptID,FSManagerID,FFManagerID,    FBillerID,FStatus,FCheckerID,FCheckDate) 
                VALUES (@FInterID,@FBillNo,'0',29,1,    CONVERT(VARCHAR(10),GETDATE(),120),@FNote,@FDeptID,@FSManagerID,@FFManagerID,   @FBillerID,1,@FBillerID,GETDATE())";

                cmdH.CommandText = strSQL;
                cmdH.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                return "no@" + ex.Message;
            }
            finally
            {
                conn.Close();
            }

            //插入表体
            conn.Open();
            SqlCommand cmdD = conn.CreateCommand();
            cmdD.CommandType = CommandType.Text;

            cmdD.Parameters.Add("@FInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@FEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@FItemID", SqlDbType.Int);
            cmdD.Parameters.Add("@FBatchNo", SqlDbType.VarChar);
            cmdD.Parameters.Add("@FQty", SqlDbType.Decimal);

            cmdD.Parameters.Add("@FUnitID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSCSPID", SqlDbType.Int);
            cmdD.Parameters.Add("@FPrice", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FAmount", SqlDbType.Decimal);

            cmdD.Parameters.Add("@FNote", SqlDbType.VarChar);

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                try
                {
                    cmdD.Parameters["@FInterID"].Value = FInterID;
                    cmdD.Parameters["@FEntryID"].Value = i + 1;

                    cmdD.Parameters["@FItemID"].Value = dtDtl.Rows[i]["FItemID"].ToString();
                    cmdD.Parameters["@FBatchNo"].Value = dtDtl.Rows[i]["FBatchNo"].ToString();
                    cmdD.Parameters["@FQty"].Value = dtDtl.Rows[i]["FQty"].ToString();
                    cmdD.Parameters["@FUnitID"].Value = dtDtl.Rows[i]["FUnitID"].ToString();
                    cmdD.Parameters["@FSCStockID"].Value = dtDtl.Rows[i]["FSCStockID"].ToString();

                    cmdD.Parameters["@FSCSPID"].Value = dtDtl.Rows[i]["FSCSPID"].ToString();
                    cmdD.Parameters["@FPrice"].Value = dtDtl.Rows[i]["FPrice"].ToString();
                    cmdD.Parameters["@FAmount"].Value = dtDtl.Rows[i]["FAmount"].ToString();
                    cmdD.Parameters["@FNote"].Value = dtDtl.Rows[i]["FNote"].ToString();

                    strSQL = @"INSERT INTO dbo.ICstockbillEntry(FInterID,FEntryID,FBrNo,FItemID,FBatchNo,FUnitID,FSCStockID,FSCSPID,FQty,FAuxQty,   FOutCommitQty,FOutSecCommitQty,FPrice,FAuxprice,FAmount,Fconsignprice,FconsignAmount,FChkPassItem,FNote)
                    VALUES(@FInterID,@FEntryID,'0',@FItemID,@FBatchNo,@FUnitID,@FSCStockID,@FSCSPID,@FQty,@FQty,    @FQty,@FQty,@FPrice,@FPrice,@FAmount,@FPrice,@FAmount,1058,@FNote)";

                    cmdD.CommandText = strSQL;
                    cmdD.ExecuteNonQuery();

                    //反写库存
                    if (FBatchManager)
                        strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
	                    SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FSCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty - DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FQty) VALUES(0,DT.FItemID,DT.FStockID,-DT.FQty);";
                    else
                        strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
	                    SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FSCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID --AND IC.FBatchNo = DT.FBatchNo
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty - DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FQty) VALUES(0,DT.FItemID,DT.FStockID,-DT.FQty);";

                    SqlOperation(0, strSQL);
                }
                catch (Exception ex)
                {
                    SqlOperation(0, "DELETE FROM ICStockBill WHERE FInterID = " + FInterID.ToString() + " DELETE FROM ICstockbillentry WHERE FInterID = " + FInterID.ToString());

                    conn.Close();
                    return "no@" + ex.Message;
                }
            }

            if (conn.State == ConnectionState.Open)
                conn.Close();

            return "yes@ID:" + FInterID.ToString() + ";Number:" + FBillNo;
        }
        #endregion

        #region 销售出库单 - 取消
        /// <summary>
        /// 销售出库单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FSEOrderBillNo|FNote</param>
        /// <param name="pDetails">表体：[FItemNumber|FDCStockNumber|FDCSPNumber|FBatchNo|FQty|FSourceBillNo|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        public static string ICStockBillForXOut(string pHead, string pDetails)
        {
            object obj;
            string strSQL;
            DataTable dtDtl;
            DataRow dr;

            int FInterID;
            string FBillNo;

            SqlConnection conn = new SqlConnection(C_CONNECTIONSTRING);
            try
            {
                conn.Open();

                //内码
                SqlCommand cmd = new SqlCommand("GetICMaxNum", conn);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@TableName", SqlDbType.VarChar, 50);
                cmd.Parameters.Add("@FInterID", SqlDbType.Int);
                cmd.Parameters.Add("@Increment", SqlDbType.Int);
                cmd.Parameters.Add("@UserID", SqlDbType.Int);

                cmd.Parameters["@TableName"].Value = "ICStockBill";
                cmd.Parameters["@FInterID"].Direction = ParameterDirection.Output;//指定参数的方向为output(返回的值)
                cmd.Parameters["@Increment"].Value = 1;
                cmd.Parameters["@UserID"].Value = 16394;

                cmd.ExecuteNonQuery();//执行这个命令

                FInterID = int.Parse(cmd.Parameters["@FInterID"].Value.ToString());

                //编号
                SqlCommand cmd2 = new SqlCommand("GetICBillNo", conn);

                cmd2.CommandType = CommandType.StoredProcedure;
                cmd2.Parameters.Add("@IsSave", SqlDbType.SmallInt);
                cmd2.Parameters.Add("@FBillType", SqlDbType.Int);
                cmd2.Parameters.Add("@BillID", SqlDbType.VarChar, 50);

                cmd2.Parameters["@IsSave"].Value = 1;
                cmd2.Parameters["@FBillType"].Value = 21;
                cmd2.Parameters["@BillID"].Direction = ParameterDirection.Output;

                cmd2.ExecuteNonQuery();

                FBillNo = cmd2.Parameters["@BillID"].Value.ToString();
            }
            catch (Exception ex)
            {
                return "no@" + ex.Message;
            }
            finally
            {
                conn.Close();
            }

            //销售订单：SEORDER
            int FOrgBillInterID, FSEOrderInterID, FSEOrderEntryID;

            //定义表头字段
            string FNote, FSEOrderBillNo, FConsignee;
            int FDeptID, FSManagerID, FFManagerID, FBillerID;

            //定义表体字段
            int FItemID, FUnitID, FSCStockID, FSCSPID;
            decimal FPrice, FQty;
            string FNoteD, FItem, FSCStock, FSCSP, FBatchNo, FSourceBillNo;

            //物料
            bool FBatchManager = false;

            //字段赋值
            try
            {
                //解释表头字段
                FDeptID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FDeptID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FSManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FFManagerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FFManagerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FBillerID = int.Parse(pHead.Substring(0, pHead.IndexOf("|")));//FBillerID
                pHead = pHead.Substring(pHead.IndexOf("|") + 1);
                FSEOrderBillNo = pHead.Substring(0, pHead.IndexOf("|"));//FSEOrderBillNo
                //
                FNote = pHead.Substring(pHead.IndexOf("|") + 1);//FNote

                //obj = SqlOperation(3, "SELECT FInterID,FBillNo,FStatus,FClosed,FCheckerID,FCustID,FMangerID,FDeptID,FBrID,FBillerID,FTranType,FConsignee FROM SEOrder WHERE FBillNo = '" + FSEBillNo + "'");
                obj = SqlOperation(3, "SELECT FInterID,FConsignee,FClosed FROM SEOrder WHERE FBillNo = '" + FSEOrderBillNo + "'");
                if (obj == null || ((DataTable)obj).Rows.Count == 0)
                    return "no@没有此单据数据[" + FSEOrderBillNo + "]";

                FConsignee = ((DataTable)obj).Rows[0]["FConsignee"].ToString();//收货方
                FOrgBillInterID = int.Parse(((DataTable)obj).Rows[0]["FInterID"].ToString());

                //源单关闭、审核和作废状态的判断-未做判断

                //解释表体字段
                dtDtl = new DataTable();
                dtDtl.Columns.Add("FItemID");
                dtDtl.Columns.Add("FUnitID");
                dtDtl.Columns.Add("FSCStockID");
                dtDtl.Columns.Add("FSCSPID");
                dtDtl.Columns.Add("FBatchNo");

                dtDtl.Columns.Add("FQty");
                dtDtl.Columns.Add("FPrice");
                dtDtl.Columns.Add("FAmount");
                dtDtl.Columns.Add("FSourceBillNo");
                dtDtl.Columns.Add("FInterID");

                dtDtl.Columns.Add("FEntryID");
                dtDtl.Columns.Add("FNote");

                string strTemp;
                do
                {
                    if (pDetails.IndexOf("],[") > 0)
                    {
                        strTemp = pDetails.Substring(0, pDetails.IndexOf("]") + 1);
                        pDetails = pDetails.Substring(pDetails.IndexOf("]") + 2);
                    }
                    else
                    {
                        strTemp = pDetails;
                        pDetails = "";
                    }

                    FItem = strTemp.Substring(1, strTemp.IndexOf("|") - 1);//MTLNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSCStock = strTemp.Substring(0, strTemp.IndexOf("|"));//FStockNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSCSP = strTemp.Substring(0, strTemp.IndexOf("|"));//FSPNumber
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FBatchNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FBatchNo
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FQty = decimal.Parse(strTemp.Substring(0, strTemp.IndexOf("|")));//FQTY

                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FSourceBillNo = strTemp.Substring(0, strTemp.IndexOf("|"));//FSourceBillNo
                    //
                    //FNoteD = strTemp.Substring(strTemp.IndexOf("|") + 1, strTemp.Length - strTemp.IndexOf("|") - 2);//FNoteD
                    strTemp = strTemp.Substring(strTemp.IndexOf("|") + 1);
                    FNoteD = strTemp.Substring(0, strTemp.IndexOf("]"));//FNoteD

                    //obj = SqlOperation(3, "SSELECT A.FInterID,AE.FEntryID,A.FBillNo 订单编号,AE.FItemID 物料ID,MTL.FUnitID 单位,MTL.FNumber 产品代码,AE.FQty 基本单位数量,AE.FStockQty 出库数量,AE.FPrice 单价,AE.FAmount 金额,AE.FCESS 税率,AE.FBatchNo 物料批号,AE.FLockFlag 锁库标志,AE.FCostObjectID 成本对象代码,AE.FOrderEntryID 订单行号 FROM SEOrder A INNER JOIN SEOrderEntry AE ON A.FInterID = AE.FInterID INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + "'");
                    obj = SqlOperation(3, "SELECT A.FInterID,AE.FEntryID,AE.FItemID,MTL.FUnitID,AE.FQty,AE.FStockQty,AE.FQty - AE.FStockQty CanOutQTY,AE.FPrice,MTL.FBatchManager FROM SEOrder A INNER JOIN SEOrderEntry AE ON A.FInterID = AE.FInterID INNER JOIN t_ICItem MTL ON AE.FItemID = MTL.FItemID WHERE A.FBillNo = '" + FSourceBillNo + "' AND MTL.FNumber = '" + FItem + "'");
                    if (obj == null || ((DataTable)obj).Rows.Count == 0)
                        //return "no@未找到源单对应的物料信息";
                        return "no@未找到物料信息[" + FSourceBillNo + "].[" + FItem + "]";

                    FSEOrderInterID = int.Parse(((DataTable)obj).Rows[0]["FInterID"].ToString());//FInterID
                    FSEOrderEntryID = int.Parse(((DataTable)obj).Rows[0]["FEntryID"].ToString());//FEntryID
                    FItemID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//MTLID
                    FUnitID = int.Parse(((DataTable)obj).Rows[0]["FUnitID"].ToString());//UnitID
                    FPrice = decimal.Parse(((DataTable)obj).Rows[0]["FPrice"].ToString());//Price

                    FBatchManager = ((DataTable)obj).Rows[0]["FBatchManager"].ToString() == "0" ? false : true;//是否采用业务批次管理

                    if (FQty > decimal.Parse(((DataTable)obj).Rows[0]["CanOutQTY"].ToString()))
                    {
                        return "no@销售订单[" + FSourceBillNo + "],产品[" + FItem + "]的可出数量小于出库数量：" + FQty + " 请核实。";
                    }

                    if (FSCStock == "")
                        FSCStockID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FItemID FROM t_Stock WHERE FNumber = '" + FSCStock + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓库信息[" + FSCStock + "]";
                        FSCStockID = int.Parse(((DataTable)obj).Rows[0]["FItemID"].ToString());//FSCStockID
                    }

                    if (FSCSP == "")
                        FSCSPID = 0;
                    else
                    {
                        obj = SqlOperation(3, "SELECT FSPID FROM t_StockPlace WHERE FNumber = '" + FSCSP + "'");
                        if (obj == null || ((DataTable)obj).Rows.Count == 0)
                            return "no@未找到仓位信息[" + FSCSP + "]";
                        FSCSPID = int.Parse(((DataTable)obj).Rows[0]["FSPID"].ToString());//FSCSPID
                    }

                    if (string.IsNullOrEmpty(FBatchNo) && FBatchManager)
                    {
                        return "no@[" + FItem + "]物料已经启用批次号管理，请携带批次号。";
                    }

                    dr = dtDtl.NewRow();

                    dr["FItemID"] = FItemID;
                    dr["FUnitID"] = FUnitID;
                    dr["FSCStockID"] = FSCStockID;
                    dr["FSCSPID"] = FSCSPID;
                    dr["FBatchNo"] = FBatchNo;

                    dr["FQty"] = FQty;
                    dr["FPrice"] = FPrice;
                    dr["FAmount"] = FQty * FPrice;
                    dr["FSourceBillNo"] = FSourceBillNo;
                    dr["FInterID"] = FSEOrderInterID;

                    dr["FEntryID"] = FSEOrderEntryID;
                    dr["FNote"] = FNoteD;

                    dtDtl.Rows.Add(dr);
                }
                while (pDetails.Length > 0);
            }
            catch (Exception ex)
            {
                return "no@" + ex.Message;
            }

            //插入主表
            try
            {
                conn.Open();
                SqlCommand cmdH = conn.CreateCommand();
                cmdH.CommandType = CommandType.Text;

                cmdH.Parameters.Add("@FInterID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillNo", SqlDbType.VarChar, 255);
                cmdH.Parameters.Add("@FNote", SqlDbType.VarChar, 255);
                cmdH.Parameters.Add("@FDeptID", SqlDbType.Int);
                cmdH.Parameters.Add("@FConsignee", SqlDbType.VarChar);

                cmdH.Parameters.Add("@FSManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FFManagerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FBillerID", SqlDbType.Int);
                cmdH.Parameters.Add("@FOrgBillInterID", SqlDbType.Int);

                cmdH.Parameters["@FInterID"].Value = FInterID;
                cmdH.Parameters["@FBillNo"].Value = FBillNo;
                cmdH.Parameters["@FNote"].Value = FNote;
                cmdH.Parameters["@FDeptID"].Value = FDeptID;
                cmdH.Parameters["@FConsignee"].Value = FConsignee;

                cmdH.Parameters["@FSManagerID"].Value = FSManagerID;
                cmdH.Parameters["@FFManagerID"].Value = FFManagerID;
                cmdH.Parameters["@FBillerID"].Value = FBillerID;
                cmdH.Parameters["@FOrgBillInterID"].Value = FOrgBillInterID;

                strSQL = @"INSERT INTO dbo.ICStockBill(FInterID,FBillNo,FBrNo,FTranType,FROB,Fdate,FNote,FDeptID,   FConsignee,FSManagerID,FFManagerID,FBillerID,FSelTranType,FOrgBillInterID,FStatus,FCheckerID,FCheckDate) 
                VALUES (@FInterID,@FBillNo,'0',21,1,CONVERT(VARCHAR(10),GETDATE(),120),@FNote,@FDeptID,  @FConsignee,@FSManagerID,@FFManagerID,@FBillerID,81,@FOrgBillInterID,1,@FBillerID,GETDATE())";

                cmdH.CommandText = strSQL;
                cmdH.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                return "no@" + ex.Message;
            }
            finally
            {
                conn.Close();
            }

            //插入表体
            conn.Open();
            SqlCommand cmdD = conn.CreateCommand();
            cmdD.CommandType = CommandType.Text;

            cmdD.Parameters.Add("@FInterID", SqlDbType.Int);
            cmdD.Parameters.Add("@FEntryID", SqlDbType.Int);
            cmdD.Parameters.Add("@FItemID", SqlDbType.Int);
            cmdD.Parameters.Add("@FBatchNo", SqlDbType.VarChar);
            cmdD.Parameters.Add("@FQty", SqlDbType.Decimal);

            cmdD.Parameters.Add("@FUnitID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSCStockID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSCSPID", SqlDbType.Int);
            cmdD.Parameters.Add("@FSourceBillNo", SqlDbType.VarChar, 50);
            cmdD.Parameters.Add("@FSourceInterId", SqlDbType.Int, 50);

            cmdD.Parameters.Add("@FSourceEntryID", SqlDbType.Int, 50);
            cmdD.Parameters.Add("@FPrice", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FAmount", SqlDbType.Decimal);
            cmdD.Parameters.Add("@FNote", SqlDbType.VarChar);

            for (int i = 0; i < dtDtl.Rows.Count; i++)
            {
                try
                {
                    cmdD.Parameters["@FInterID"].Value = FInterID;
                    cmdD.Parameters["@FEntryID"].Value = i + 1;
                    cmdD.Parameters["@FItemID"].Value = dtDtl.Rows[i]["FItemID"].ToString();
                    cmdD.Parameters["@FBatchNo"].Value = dtDtl.Rows[i]["FBatchNo"].ToString();
                    cmdD.Parameters["@FQty"].Value = dtDtl.Rows[i]["FQty"].ToString();

                    cmdD.Parameters["@FUnitID"].Value = dtDtl.Rows[i]["FUnitID"].ToString();
                    cmdD.Parameters["@FSCStockID"].Value = dtDtl.Rows[i]["FSCStockID"].ToString();
                    cmdD.Parameters["@FSCSPID"].Value = dtDtl.Rows[i]["FSCSPID"].ToString();
                    cmdD.Parameters["@FSourceBillNo"].Value = dtDtl.Rows[i]["FSourceBillNo"].ToString();
                    cmdD.Parameters["@FSourceInterId"].Value = dtDtl.Rows[i]["FInterID"].ToString();

                    cmdD.Parameters["@FSourceEntryID"].Value = dtDtl.Rows[i]["FEntryID"].ToString();
                    cmdD.Parameters["@FPrice"].Value = dtDtl.Rows[i]["FPrice"].ToString();
                    cmdD.Parameters["@FAmount"].Value = dtDtl.Rows[i]["FAmount"].ToString();
                    cmdD.Parameters["@FNote"].Value = dtDtl.Rows[i]["FNote"].ToString();

                    strSQL = @"INSERT INTO dbo.ICstockbillEntry(FInterID,FEntryID,FBrNo,FItemID,FBatchNo,FUnitID,FSCStockID,FSCSPID,FQty,FAuxQty,   FOutCommitQty,FOutSecCommitQty,FPrice,FAuxprice,FAmount,Fconsignprice,FconsignAmount,FSCBillNo,FSCBillInterID,FSourceBillNo,    FSourceInterId,FSourceEntryID,FSourceTranType,FChkPassItem,FNote)
                    VALUES(@FInterID,@FEntryID,'0',@FItemID,@FBatchNo,@FUnitID,@FSCStockID,@FSCSPID,@FQty,@FQty,    @FQty,@FQty,@FPrice,@FPrice,@FAmount,@FPrice,@FAmount,@FSourceBillNo,@FSourceInterId,@FSourceBillNo,    @FSourceInterId,@FSourceEntryID,81,1058,@FNote)";

                    cmdD.CommandText = strSQL;
                    cmdD.ExecuteNonQuery();

                    //反写库存和销售订单
                    if (FBatchManager)
                        strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
	                    SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FSCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID AND IC.FBatchNo = DT.FBatchNo
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty - DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FQty) VALUES(0,DT.FItemID,DT.FStockID,DT.FQty);
                    UPDATE SEOrderEntry SET FStockQty =  FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FInterID"].ToString() + " AND FEntryID = " + dtDtl.Rows[i]["FEntryID"].ToString() + ";";
                    else
                        strSQL = @"MERGE INTO ICInventory AS IC
                    USING
                    (
	                    SELECT " + dtDtl.Rows[i]["FItemID"].ToString() + " FItemID, " + dtDtl.Rows[i]["FSCStockID"].ToString() + " FStockID," + dtDtl.Rows[i]["FQty"].ToString() + " FQty,'" + dtDtl.Rows[i]["FBatchNo"].ToString() + @"' FBatchNo
                    ) AS DT ON IC.FItemID = DT.FItemID AND IC.FStockID = DT.FStockID --AND IC.FBatchNo = DT.FBatchNo
                    WHEN MATCHED
                        THEN UPDATE SET FQty = IC.FQty - DT.FQty
                    WHEN NOT MATCHED
                        THEN INSERT(FBrNo,FItemID,FStockID,FQty) VALUES(0,DT.FItemID,DT.FStockID,-DT.FQty);
                    UPDATE SEOrderEntry SET FStockQty =  FStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + ",FAuxStockQty = FAuxStockQty + " + dtDtl.Rows[i]["FQty"].ToString() + " WHERE FInterID = " + dtDtl.Rows[i]["FInterID"].ToString() + " AND FEntryID = " + dtDtl.Rows[i]["FEntryID"].ToString() + ";";

                    SqlOperation(0, strSQL);
                }
                catch (Exception ex)
                {
                    SqlOperation(0, "DELETE FROM ICStockBill WHERE FInterID = " + FInterID.ToString() + " DELETE FROM ICstockbillentry WHERE FInterID = " + FInterID.ToString());

                    conn.Close();
                    return "no@" + ex.Message;
                }
            }

            if (conn.State == ConnectionState.Open)
                conn.Close();

            return "yes@ID:" + FInterID.ToString() + ";Number:" + FBillNo;
        }
        #endregion
        #endregion

        //-----Private Members
        #region Private
        /// <summary>
        /// 获取ICStockBillNo的最大内码和新的单据编码
        /// </summary>
        /// <param name="pFBillType">FTranType</param>
        /// <param name="pFInterID">最大内码</param>
        /// <param name="pFBillNo">新的单据编码</param>
        private static void GetICMaxIDAndBillNo(int pFBillType, out int pFInterID, out string pFBillNo)
        {
            SqlConnection conn = new SqlConnection(C_CONNECTIONSTRING);
            try
            {
                conn.Open();
                //内码
                SqlCommand cmd = new SqlCommand("GetICMaxNum", conn);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@TableName", SqlDbType.VarChar, 50);
                cmd.Parameters.Add("@FInterID", SqlDbType.Int);
                cmd.Parameters.Add("@Increment", SqlDbType.Int);
                cmd.Parameters.Add("@UserID", SqlDbType.Int);

                cmd.Parameters["@TableName"].Value = "ICStockBill";
                cmd.Parameters["@FInterID"].Direction = ParameterDirection.Output;//指定参数的方向为output(返回的值)
                cmd.Parameters["@Increment"].Value = 1;
                cmd.Parameters["@UserID"].Value = 16394;

                cmd.ExecuteNonQuery();//执行这个命令

                pFInterID = int.Parse(cmd.Parameters["@FInterID"].Value.ToString());

                //编号
                SqlCommand cmd2 = new SqlCommand("DM_GetICBillNo", conn);

                cmd2.CommandType = CommandType.StoredProcedure;
                cmd2.Parameters.Add("@FBillType", SqlDbType.Int);
                cmd2.Parameters.Add("@BillNo", SqlDbType.VarChar, 50);

                cmd2.Parameters["@FBillType"].Value = pFBillType;
                cmd2.Parameters["@BillNo"].Direction = ParameterDirection.Output;

                cmd2.ExecuteNonQuery();

                pFBillNo = cmd2.Parameters["@BillNo"].Value.ToString();
            }
            catch (Exception ex)
            {
                pFInterID = 0;
                pFBillNo = "Error:" + ex.Message;
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        /// 判断DataTable中某一列是否包含某个值
        /// </summary>
        /// <param name="pDataTable">DataTable</param>
        /// <param name="pColumnName">指定列</param>
        /// <param name="pValue">指定值</param>
        /// <returns></returns>
        private static bool ContainValue(DataTable pDataTable, string pColumnName, string pValue)
        {
            if (pDataTable == null || pDataTable.Rows.Count == 0 || !pDataTable.Columns.Contains(pColumnName)) return false;

            for (int i = 0; i < pDataTable.Rows.Count; i++) if (pDataTable.Rows[i][pColumnName].ToString() == pValue) return true;

            return false;
        }

        /// <summary>
        /// 获取DataTable 前pIndex行的Sum
        /// </summary>
        /// <param name="pDataTable">DataTable</param>
        /// <param name="pColumnName">统计列</param>
        /// <param name="pIndex">序号</param>
        /// <returns></returns>
        private static decimal SumPre(DataTable pDataTable, string pColumnName, int pIndex)
        {
            if (pDataTable == null || pDataTable.Rows.Count == 0 || pIndex == 0) return 0;

            decimal dSum = 0;

            if (pIndex > pDataTable.Rows.Count - 1) pIndex = pDataTable.Rows.Count - 1;

            if (!pDataTable.Columns.Contains(pColumnName)) return 0;

            for (int i = 0; i < pIndex; i++) dSum += decimal.Parse(pDataTable.Rows[i][pColumnName].ToString());

            return dSum;
        }

        /// <summary>
        /// 获取物料的本次入库总数
        /// </summary>
        /// <param name="pDataTable">DataTable</param>
        /// <param name="pMTL">物料编码</param>
        /// <returns></returns>
        private static decimal GetTotalQty(DataTable pDataTable, string pMTL)
        {
            if (pDataTable == null || pDataTable.Rows.Count == 0) return 0;

            decimal pTotal = 0;

            for (int i = 0; i < pDataTable.Rows.Count; i++)
            {
                if (pDataTable.Rows[i]["FItem"].ToString() == pMTL)
                {
                    pTotal = decimal.Parse(pDataTable.Rows[i]["TotalQty"].ToString());
                    break;
                }
            }

            return pTotal;
        }

        /// <summary>
        /// 更新DataTable
        /// </summary>
        /// <param name="pDataTable">DataTable</param>
        /// <param name="pColMatch">匹配列</param>
        /// <param name="pValueMatch">匹配值</param>
        /// <param name="pColSet">更新列</param>
        /// <param name="pValueSet">更新值</param>
        private static void UpdateTable(DataTable pDataTable, string pColMatch, string pValueMatch, string pColSet, decimal pValueSet)
        {
            if (pDataTable == null || pDataTable.Rows.Count == 0) return;

            for (int i = 0; i < pDataTable.Rows.Count; i++) if (pDataTable.Rows[i][pColMatch].ToString() == pValueMatch) pDataTable.Rows[i][pColSet] = pValueSet;
        }
        #endregion

        //-----SQL Helper
        #region 数据库操作
        /// <summary>
        /// 数据库操作
        /// </summary>
        /// <param name="pType">0、NonQuery;1、Scalar;2、Reader;3、DataTable;4、DataSet</param>
        /// <param name="pSQL">SQL Sentence</param>
        /// <returns></returns>
        private static object SqlOperation(int pType, string pSQL)
        {
            object obj;
            SqlDataAdapter adp;
            DataTable dt;
            DataSet ds;

            SqlConnection conn = new SqlConnection(C_CONNECTIONSTRING);

            try
            {
                conn.Open();
                SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = pSQL;

                switch (pType)
                {
                    case 0:
                        obj = cmd.ExecuteNonQuery();
                        break;
                    case 1:
                        obj = cmd.ExecuteScalar();
                        break;
                    case 2:
                        obj = cmd.ExecuteReader();
                        break;
                    case 3:
                        dt = new DataTable();
                        adp = new SqlDataAdapter(pSQL, conn);
                        adp.Fill(dt);
                        obj = dt;
                        break;
                    case 4:
                        ds = new DataSet();
                        adp = new SqlDataAdapter(pSQL, conn);
                        adp.Fill(ds);
                        obj = ds;
                        break;
                    default:
                        obj = null;
                        break;
                }
            }
            catch { return null; }
            finally
            {
                conn.Close();
            }

            return obj;
        }
        #endregion
    }
}