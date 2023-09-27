using System;
using Serilog;

namespace isci.msb
{
    public class MsbClientFP : Fraunhofer.IPA.MSB.Client.Websocket.MsbClient
    {
        Fraunhofer.IPA.MSB.Client.API.Model.Service service;
        public MsbClientFP(string target_interface) : base(target_interface)
        {

        }

        private new void HandleFunctionCall(Fraunhofer.IPA.MSB.Client.API.Model.FunctionCall functionCall)
        {            
            Log.Debug($"Callback for function '{functionCall.FunctionId}' of service '{functionCall.ServiceUuid}' received with parameters: {Newtonsoft.Json.JsonConvert.SerializeObject(functionCall.FunctionParameters)}");

            var serviceOfFunctionCall = service;
            var calledFunction = serviceOfFunctionCall.GetFunctionById(functionCall.FunctionId);

            if (serviceOfFunctionCall.Functions.Find(function => function.Id == functionCall.FunctionId) is Fraunhofer.IPA.MSB.Client.API.Model.Function functionOfService)
            {
                var pointer = functionOfService.FunctionPointer;

                var functionCallInfo = new Fraunhofer.IPA.MSB.Client.API.Model.FunctionCallInfo(this, functionCall.CorrelationId, service, calledFunction, null);

                var parameterArrayForInvoke = new object[2];
                parameterArrayForInvoke[0] = functionCall.FunctionParameters;
                parameterArrayForInvoke[parameterArrayForInvoke.Length - 1] = functionCallInfo;
                try
                {
                    var returnValue = functionOfService.FunctionPointer.DynamicInvoke(parameterArrayForInvoke);
                    if (returnValue != null)
                    {
                    }
                } catch
                {

                }
            }
        }
    }
}