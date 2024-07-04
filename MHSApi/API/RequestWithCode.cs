/*
 * PROJECT: SecuritySystem
 * DATE: 2/6/2024
 * DESCRIPTION: Contains ARM api content
*/

namespace MHSApi.API
{
    /// <summary>
    /// Represents the JSON request to the arm/disarm endpoint
    /// </summary>
    public class RequestWithCode
    {
        /// <summary>
        /// represents the code in plain text
        /// </summary>
        public string code { get; set; }

        public RequestWithCode(string code)
        {
            this.code = code;
        }
    }
}
