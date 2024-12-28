/*
 * PROJECT: SecuritySystem
 * DATE: 2/6/2024
 * DESCRIPTION: Contains core SecuritySystem HTTP api requests/responses
*/

using MHSApi.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace SecuritySystemApi
{  /// <summary>
   /// Represents the basic response of the SecuritySystem API
   /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// True if request succeeded, false if failure. See message/code properties for further information
        /// </summary>
        public bool success { get; set; }
        /// <summary>
        /// Error message
        /// </summary>
        public string message { get; set; } = "";
        /// <summary>
        /// Error code
        /// </summary>
        public SecurityApiResult code { get; set; }

        public ApiResponse() { }
    }
    /// <summary>
    /// Represents the basic response of the SecuritySystem API with content
    /// </summary>
    /// <typeparam name="T">The response type, ie UpdateInformation</typeparam>
    public class ApiResponseWithContent<T> : ApiResponse
    {
        public T? content { get; set; }

        public ApiResponseWithContent() { }
    }
    /// <summary>
    /// Represents the content field in an ApiResponse
    /// </summary>
    public class ApiResponseContent
    {

    }

    /// <summary>
    /// Represents the content of the UpdateInformation API
    /// </summary>
    public class UpdateInformationContent : ApiResponseContent
    {
        public string? serverVersion { get; set; }
        public string? changelog { get; set; }
        public UpdateFiles? files { get; set; }
    }
    /// <summary>
    /// Represents the files object in UpdateInformation
    /// </summary>
    public class UpdateFiles
    {
        public string[]? linux64 { get; set; }
        public string[]? win64 { get; set; }
    }
    /// <summary>
    /// Represents the content of the GetAlarmHistory api
    /// </summary>
    public class AlarmHistoryInfoContent : ApiResponseContent
    {
        /// <summary>
        /// The date of the alarm returned by the server
        /// </summary>
        public string date { get; set; } = "";
        /// <summary>
        /// The zone number
        /// </summary>
        public int zone { get; set; } = 0;
        /// <summary>
        /// The date of the alarm as a DateTime object
        /// </summary>
        public DateTime Date
        {
            get
            {
                return DateTime.Parse(date);
            }
        }
    }
    /// <summary>
    /// Represents the content of the GetEventLog api
    /// </summary>
    public class EventLogEntry : ApiResponseContent
    {
        /// <summary>
        /// The date of the alarm returned by the server
        /// </summary>
        public string date { get; set; } = "";
        /// <summary>
        /// The zone number
        /// </summary>
        public string Message { get; set; } = "";
        /// <summary>
        /// user that preformed modification
        /// </summary>
        public string Username { get; set; } = "";
        /// <summary>
        /// The date of the alarm as a DateTime object
        /// </summary>
        public DateTime Date
        {
            get
            {
                return DateTime.Parse(date);
            }
        }
    }
}
