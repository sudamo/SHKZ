using System.Data;
using System.Web.Services;

namespace SHKZWebService
{
    /// <summary>
    /// WebService 的摘要说明
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // 若要允许使用 ASP.NET AJAX 从脚本中调用此 Web 服务，请取消注释以下行。 
    // [System.Web.Script.Services.ScriptService]
    public class CBSW : WebService
    {
        /// <summary>
        /// 连接测试
        /// </summary>
        /// <returns>连接成功/连接失败</returns>
        [WebMethod]
        public string TestConnection()
        {
            return Common.TestConnection();
        }

        /// <summary>
        /// 审核单据(目前只支持 出入库 ICStockBill 单据)
        /// </summary>
        /// <param name="pFBillNo">单号</param>
        /// <param name="pFCheckerID">审核人ID</param>
        /// <returns>审核成功/审核失败：失败信息</returns>
        [WebMethod]
        public string AuditBill(string pFBillNo, int pFCheckerID)
        {
            return Common.AuditBill(pFBillNo, pFCheckerID);
        }

        /// <summary>
        /// 生产入库单
        /// </summary>
        /// <param name="pHead"></param>
        /// <param name="pDetail"></param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        [WebMethod]
        public string ICStockBillForInStock(string pHead, string pDetail)
        {
            return Common.ICStockBillForInStock(pHead, pDetail);
        }

        /// <summary>
        /// 外购入库单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FPOOrderBillNo|FNote</param>
        /// <param name="pDetails">表体：[FItemNumber|FDCStockNumber|FDCSPNumber|FBatchNo|FQty|FSourceBillNo|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        [WebMethod]
        public string ICStockBillForPO(string pHead, string pDetails)
        {
            return Common.ICStockBillForPO(pHead, pDetails);
        }

        /// <summary>
        /// 外购入库单2
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FPOOrderBillNo|FNote</param>
        /// <param name="pDetails">表体：[FItemNumber|FDCStockNumber|FDCSPNumber|FBatchNo|FQty|FSourceBillNo|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        [WebMethod]
        public string ICStockBillForPO2(string pHead, string pDetails)
        {
            return Common.ICStockBillForPO2(pHead, pDetails);
        }

        /// <summary>
        /// 生产领料单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FNote</param>
        /// <param name="pDetails">表体：[FItemNumber|FSCStockNumber|FSCSPNumber|FBatchNo|FQty|FSourceBillNo|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        [WebMethod]
        public string ICStockBillForPick(string pHead, string pDetails)
        {
            return Common.ICStockBillForPick(pHead, pDetails);
        }

        /// <summary>
        /// 调拨单
        /// </summary>
        /// <param name="pHead">表头：FDeptID|FSManagerID|FFManagerID|FBillerID|FCheckerID</param>
        /// <param name="pDetail">表体：[FItemNumber|FSCStockNumber|FSCSPNumber|FBatchNo|FQty|FDCStockNumber|FDCSPNumber|FNote],......</param>
        /// <returns>yes@ID:xxxx;Number:xxxx/no@ExceptionMessage</returns>
        [WebMethod]
        public string ICStockBillForTrans(string pHead, string pDetails)
        {
            return Common.ICStockBillForTrans(pHead, pDetails);
        }
    }
}
