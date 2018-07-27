using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Services;
using Microsoft.Xrm.Sdk;

namespace LinkDev.DataMigration.WebApp.Controllers
{
	public class LogActionFilter : ActionFilterAttribute
	{
		public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
		{
			var controller = actionExecutedContext.ActionContext.ControllerContext.Controller;
			var controllerType = controller.GetType();
			var logField = controllerType.GetField("log", BindingFlags.NonPublic | BindingFlags.Instance);
			var logMethod = logField?.FieldType.GetMethod("LogExecutionEnd",
				new[]
				{
					typeof(bool), typeof(CrmLog.ExecutionEndState), typeof(LogEntry), typeof(IExecutionContext),
					typeof(string), typeof(string), typeof(string), typeof(int)
				});
			logMethod?.Invoke(logField.GetValue(controller), new object[] { true, 0, null, null, null, null, "", 0 });
		}
	}

	public class ApiControllerBase : ApiController
	{
	    protected IEnhancedOrgService service;
	    protected CrmLog log;

		public ApiControllerBase(IEnhancedOrgService service, CrmLog log)
		{
			this.service = service;
			this.log = log;
		}
	}
}
