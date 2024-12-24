#pragma warning disable CA1822, S2325 // Mark members as static
using G4.Attributes;
using G4.Extensions;
using G4.Models;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace G4.UnitTests.Framework
{
    /// <summary>
    /// Controller for hotel search related operations.
    /// </summary>
    [Route("api/hotels")]
    [ApiController]
    public class HotelSearchController(ILogger<HotelSearchController> logger) : ControllerBase
    {
        // Logger for capturing and logging messages related to hotel search operations.
        private readonly ILogger<HotelSearchController> _logger = logger;

        #region *** Controller.Delete ***
        /// <summary>
        /// Cancels a hotel booking based on the provided booking ID.
        /// </summary>
        /// <param name="bookingId">The unique identifier for the hotel booking to be canceled.</param>
        /// <returns>A JsonResult containing a message indicating whether the hotel booking was canceled successfully or the booking ID is invalid.</returns>
        /// <remarks>
        /// This action method simulates the cancellation of a hotel booking based on the provided booking ID.
        /// If the booking ID is "12345," it confirms the cancellation.
        /// Otherwise, it returns a message indicating that the booking ID is invalid.
        /// The response is returned as a JsonResult with an optional status code.
        /// </remarks>
        [HttpDelete("delete/{bookingId}")]
        public IActionResult DeleteBookRequest(string bookingId)
        {
            try
            {
                // Log the start of the hotel booking cancellation action
                _logger.LogInformation("Attempting hotel booking cancellation for BookingID: {bookingId}", bookingId);

                // Check if the provided booking ID is "12345"
                if (bookingId == "12345")
                {
                    // If yes, create a response indicating the successful cancellation
                    var response = new { Message = "Booking canceled successfully" };

                    // Log the successful hotel booking cancellation
                    _logger.LogInformation("Hotel booking canceled successfully.");

                    // Return a JsonResult with the confirmation message
                    return new JsonResult(response);
                }
                else
                {
                    // If no, create a response indicating that the booking ID is invalid
                    var response = new { Message = "Invalid booking ID" };

                    // Log that the booking ID is invalid
                    _logger.LogInformation("Invalid booking ID.");

                    // Return a JsonResult with the failure message and status code 400 (Bad Request)
                    return new JsonResult(response)
                    {
                        StatusCode = 400
                    };
                }
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception during the hotel booking cancellation
                _logger.LogError("An error occurred during hotel booking cancellation: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return StatusCode(500, "An error occurred during the hotel booking cancellation.");
            }
        }
        #endregion

        #region *** Controller.Get    ***
        /// <summary>
        /// Searches for hotels based on the specified parameters.
        /// </summary>
        /// <param name="destination">The destination for the hotel search.</param>
        /// <param name="adults">The number of adults in the search party.</param>
        /// <param name="children">The number of children in the search party.</param>
        /// <returns>A JSON result containing hotel search results.</returns>
        /// <remarks>
        /// This action method performs a hotel search based on the provided parameters.
        /// The search is initiated with the specified destination, number of adults, and number of children.
        /// The method returns a JSON result containing the search results.
        /// </remarks>
        [HttpGet("find")]
        public IActionResult FindHotelsJson(string destination, int adults, int children)
        {
            return FindHotels(
                mediaType: MediaTypeNames.Application.Json,
                destination,
                adults,
                children,
                _logger);
        }

        /// <summary>
        /// Searches for hotels based on the specified parameters and returns the results in XML format.
        /// </summary>
        /// <param name="destination">The destination for the hotel search.</param>
        /// <param name="adults">The number of adults in the search party.</param>
        /// <param name="children">The number of children in the search party.</param>
        /// <returns>A ContentResult containing the hotel search results in XML format.</returns>
        /// <remarks>
        /// This action method performs a hotel search based on the provided parameters.
        /// The search is initiated with the specified destination, number of adults, and number of children.
        /// The method then converts the search results to XML format using the ConvertToXml extension method.
        /// The XML content is returned in a ContentResult with the appropriate content type (text/xml).
        /// </remarks>
        [HttpGet("find/xml")]
        public IActionResult FindHotelsXml(string destination, int adults, int children)
        {
            return FindHotels(
                mediaType: MediaTypeNames.Text.Xml,
                destination,
                adults,
                children,
                _logger);
        }

        // Searches for hotels based on the specified parameters and returns the results in the specified format.
        private static IActionResult FindHotels(
            string mediaType, string destination, int adults, int children, ILogger logger)
        {
            try
            {
                // Log the start of the XML hotel search action
                logger.LogInformation("Attempting {mediaType} hotel search for " +
                    "Destination: {destination}, " +
                    "Adults: {adults}, " +
                    "Children: {children}", mediaType, destination, adults, children);

                // Get sample search results (replace with actual search logic)
                var content = "";
                if (mediaType == MediaTypeNames.Application.Json)
                {
                    content = GetSampleSearchResults(destination, adults, children).ConvertToJson();
                }
                if (mediaType == MediaTypeNames.Text.Xml)
                {
                    content = GetSampleSearchResults(destination, adults, children).ConvertToXml();
                }

                // Log the successful completion of the XML hotel search
                logger.LogInformation("{mediaType} hotel search completed successfully.", mediaType);

                // Return the XML content as a ContentResult with the appropriate content type
                return new ContentResult
                {
                    Content = content,
                    ContentType = mediaType
                };
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception during the search
                logger.LogError("An error occurred during {mediaType} hotel search: {Message}", mediaType, e.Message);

                // Return a StatusCode 500 response with an error message
                return new ObjectResult("An error occurred during the hotel booking.")
                {
                    StatusCode = 500
                };
            }
        }

        /// <summary>
        /// Gets the request headers.
        /// </summary>
        /// <returns>An IActionResult containing an anonymous object with the request headers.</returns>
        /// <remarks>
        /// This action retrieves all the headers from the request and returns them
        /// as part of an anonymous object in the response message.
        /// </remarks>
        [HttpGet("headers")]
        public IActionResult GetHeaders()
        {
            // Log the start of the headers action
            _logger.LogInformation("Attempting to retrieve request headers.");

            // Get all the headers from the request
            var requestHeaders = Request.Headers;

            // Log the retrieved headers
            _logger.LogInformation("Request headers retrieved.");

            // Return an anonymous object with the request headers in the response message
            return Ok(new
            {
                Headers = requestHeaders
            });
        }
        #endregion

        #region *** Controller.Patch  ***
        /// <summary>
        /// Posts a review for a hotel based on the provided review request.
        /// </summary>
        /// <param name="request">The hotel review request containing details such as the hotel name to review.</param>
        /// <returns>A JsonResult containing a message indicating whether the hotel review was posted successfully or the hotel was not found.</returns>
        /// <remarks>
        /// This action method simulates posting a review for a hotel based on the provided HotelReviewRequest.
        /// If the hotel name is "Luxury Hotel," it confirms the review posting.
        /// Otherwise, it returns a message indicating that the hotel was not found for review.
        /// The response is returned as a JsonResult with an optional status code.
        /// </remarks>
        [HttpPatch("update")]
        public IActionResult UpdateBookRequestJson([FromBody] HotelReviewRequest request)
        {
            return UpdateBookRequest(request, _logger);
        }

        /// <summary>
        /// Posts a review for a hotel based on the provided review request.
        /// </summary>
        /// <returns>A JsonResult containing a message indicating whether the hotel review was posted successfully or the hotel was not found.</returns>
        /// <remarks>
        /// This action method simulates posting a review for a hotel based on the provided HotelReviewRequest.
        /// If the hotel name is "Luxury Hotel," it confirms the review posting.
        /// Otherwise, it returns a message indicating that the hotel was not found for review.
        /// The response is returned as a JsonResult with an optional status code.
        /// </remarks>
        [HttpPatch("update/text"), Consumes(MediaTypeNames.Text.Plain)]
        public IActionResult UpdateBookRequestText()
        {
            // Read the request body as a string
            var input = Request.Body.ReadAsStringAsync().Result;

            // Extract hotel name and room type using regular expressions
            var request = new HotelReviewRequest
            {
                HotelName = Regex.Match(input, pattern: "Luxury Hotel").Value,
                Review = Regex.Match(input, pattern: "Suite").Value
            };

            // Submit the hotel booking request and return the result
            return UpdateBookRequest(request, _logger);
        }

        /// <summary>
        /// Posts a review for a hotel based on the provided review request.
        /// </summary>
        /// <param name="request">The hotel review request containing details such as the hotel name to review.</param>
        /// <returns>A JsonResult containing a message indicating whether the hotel review was posted successfully or the hotel was not found.</returns>
        /// <remarks>
        /// This action method simulates posting a review for a hotel based on the provided HotelReviewRequest.
        /// If the hotel name is "Luxury Hotel," it confirms the review posting.
        /// Otherwise, it returns a message indicating that the hotel was not found for review.
        /// The response is returned as a JsonResult with an optional status code.
        /// </remarks>
        [HttpPatch("update/encoded"), Consumes(MediaTypeNames.Application.FormUrlEncoded)]
        public IActionResult UpdateBookRequestUrlEncoded([FromForm] HotelReviewRequest request)
        {
            return UpdateBookRequest(request, _logger);
        }

        /// <summary>
        /// Posts a review for a hotel based on the provided review request.
        /// </summary>
        /// <param name="request">The hotel review request containing details such as the hotel name to review.</param>
        /// <returns>A JsonResult containing a message indicating whether the hotel review was posted successfully or the hotel was not found.</returns>
        /// <remarks>
        /// This action method simulates posting a review for a hotel based on the provided HotelReviewRequest.
        /// If the hotel name is "Luxury Hotel," it confirms the review posting.
        /// Otherwise, it returns a message indicating that the hotel was not found for review.
        /// The response is returned as a JsonResult with an optional status code.
        /// </remarks>
        [HttpPatch("update/xml"), Consumes(MediaTypeNames.Text.Xml)]
        public IActionResult UpdateBookRequestXml([FromBody] HotelReviewRequest request)
        {
            return UpdateBookRequest(request, _logger);
        }

        private static IActionResult UpdateBookRequest(HotelReviewRequest request, ILogger logger)
        {
            try
            {
                // Log the start of the hotel review posting action
                logger.LogInformation("Attempting hotel review posting for HotelName: {HotelName}", request.HotelName);

                // Check if the provided hotel name is "Luxury Hotel"
                if (request.HotelName == "Luxury Hotel")
                {
                    // If yes, create a response indicating the successful review posting
                    var response = new { Message = "Review posted for Luxury Hotel" };

                    // Log the successful hotel review posting
                    logger.LogInformation("Hotel review posted successfully.");

                    // Return a JsonResult with the confirmation message
                    return new JsonResult(response);
                }
                else
                {
                    // If no, create a response indicating that the hotel was not found for review
                    var response = new { Message = "Hotel not found for review" };

                    // Log that the hotel was not found for review
                    logger.LogInformation("Hotel not found for review.");

                    // Return a JsonResult with the failure message and status code 404 (Not Found)
                    return new JsonResult(response)
                    {
                        StatusCode = 404
                    };
                }
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception during the hotel review posting
                logger.LogError("An error occurred during hotel review posting: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return new ObjectResult("An error occurred during the hotel booking.")
                {
                    StatusCode = 500
                };
            }
        }
        #endregion

        #region *** Controller.Post   ***
        /// <summary>
        /// Gets the login information from the request headers.
        /// </summary>
        /// <returns>
        /// An IActionResult containing an anonymous object with a message containing the Authorization header from the request.
        /// </returns>
        /// <remarks>
        /// This action retrieves the Authorization header from the request headers
        /// and returns it as part of an anonymous object in the response message.
        /// </remarks>
        [HttpPost("connect")]
        public IActionResult Connect()
        {
            // Log the start of the login action
            _logger.LogInformation("Attempting to retrieve login information.");

            // Get the Authorization header from the request headers
            var authorizationHeader = Request.Headers.Authorization;

            // Log the retrieved login information
            _logger.LogInformation("Login information retrieved. Authorization Header: {authorizationHeader}", authorizationHeader);

            // Return an anonymous object with the login information in the response message
            return Ok(new
            {
                Message = $"{authorizationHeader}"
            });
        }

        /// <summary>
        /// Books a hotel based on the provided booking request.
        /// </summary>
        /// <param name="request">The hotel booking request containing details such as hotel name and room type.</param>
        /// <returns>
        /// A JsonResult containing a message indicating whether the booking was confirmed or failed.
        /// </returns>
        /// <remarks>
        /// This action method simulates hotel booking logic based on the provided HotelBookingRequest.
        /// If the hotel name is "Luxury Hotel" and the room type is "Suite," it confirms the booking.
        /// Otherwise, it returns a failure message. The response is returned as a JsonResult.
        /// </remarks>
        [HttpPost("book")]
        public IActionResult SubmitBookRequestJson([FromBody] HotelBookingRequest request)
        {
            return SubmitBookRequest(request, _logger);
        }

        /// <summary>
        /// Books a hotel using XML content and returns the booking confirmation or failure response.
        /// </summary>
        /// <param name="request">The hotel booking request in XML format.</param>
        /// <returns>An XML response containing the booking confirmation or failure message.</returns>
        /// <remarks>
        /// This action simulates hotel booking logic based on the provided HotelBookingRequest.
        /// If the hotel name is "Luxury Hotel" and the room type is "Suite," it confirms the booking.
        /// Otherwise, it returns a failure message. The response is formatted as XML.
        /// </remarks>
        [HttpPost("book/xml"), Consumes(MediaTypeNames.Text.Xml)]
        public IActionResult SubmitBookRequestXml([FromBody] HotelBookingRequest request)
        {
            return SubmitBookRequest(request, _logger);
        }

        /// <summary>
        /// Books a hotel using Text content and returns the booking confirmation or failure response.
        /// </summary>
        /// <returns>An XML response containing the booking confirmation or failure message.</returns>
        /// <remarks>
        /// This action simulates hotel booking logic based on the provided HotelBookingRequest.
        /// If the hotel name is "Luxury Hotel" and the room type is "Suite," it confirms the booking.
        /// Otherwise, it returns a failure message. The response is formatted as XML.
        /// </remarks>
        [HttpPost("book/text"), Consumes(MediaTypeNames.Text.Plain)]
        public IActionResult SubmitBookRequestText()
        {
            // Read the request body as a string
            var input = Request.Body.ReadAsStringAsync().Result;

            // Extract hotel name and room type using regular expressions
            var request = new HotelBookingRequest
            {
                HotelName = Regex.Match(input, pattern: "Luxury Hotel").Value,
                RoomType = Regex.Match(input, pattern: "Suite").Value
            };

            // Submit the hotel booking request and return the result
            return SubmitBookRequest(request, _logger);
        }

        /// <summary>
        /// Endpoint for booking a hotel with form-urlencoded data.
        /// </summary>
        /// <param name="request">The hotel booking request with form-urlencoded data.</param>
        /// <returns>
        /// Returns a ContentResult with XML content, OK status code, and XML content type if the booking is successful.
        /// Returns a ContentResult with XML content, Bad Request status code, and XML content type if the booking fails.
        /// </returns>
        /// <remarks>
        /// This action simulates hotel booking logic based on the provided HotelBookingRequest.
        /// If the hotel name is "Luxury Hotel" and the room type is "Suite," it confirms the booking.
        /// Otherwise, it returns a failure message. The response is formatted as XML.
        /// </remarks>
        [HttpPost("book/encoded"), Consumes(MediaTypeNames.Application.FormUrlEncoded)]
        public IActionResult SubmitBookRequestUrlEncoded([FromForm] HotelBookingRequest request)
        {
            return SubmitBookRequest(request, _logger);
        }

        // Books a hotel using request object and returns the booking confirmation or failure response.
        private static IActionResult SubmitBookRequest(HotelBookingRequest request, ILogger logger)
        {
            try
            {
                // Log the start of the hotel booking action
                logger.LogInformation("Attempting hotel booking for " +
                    "HotelName: {HotelName}, " +
                    "RoomType: {RoomType}", request.HotelName, request.RoomType);

                if (request.HotelName == "Luxury Hotel" && request.RoomType == "Suite")
                {
                    // Booking is confirmed for Luxury Hotel, Suite
                    var response = new { Message = "Booking confirmed for Luxury Hotel, Suite" };

                    // Log the successful hotel booking
                    logger.LogInformation("Hotel booking confirmed.");

                    // Return a JsonResult with the confirmation message
                    return new JsonResult(response);
                }
                else
                {
                    // Booking failed for any other hotel name or room type
                    var response = new { Message = "Booking failed" };

                    // Log the failed hotel booking
                    logger.LogInformation("Hotel booking failed.");

                    // Return a JsonResult with the failure message and status code 400 (Bad Request)
                    return new JsonResult(response)
                    {
                        StatusCode = 400
                    };
                }
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception during the hotel booking
                logger.LogError("An error occurred during hotel booking: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return new ObjectResult("An error occurred during the hotel booking.")
                {
                    StatusCode = 500
                };
            }
        }
        #endregion

        #region *** Controller.Put    ***
        /// <summary>
        /// Updates hotel information based on the provided update request.
        /// </summary>
        /// <param name="request">The hotel update request containing details such as the hotel name to update.</param>
        /// <returns>
        /// A JsonResult containing a message indicating whether the hotel information was updated or not found.
        /// </returns>
        /// <remarks>
        /// This action method updates hotel information based on the provided HotelUpdateRequest.
        /// If the hotel name is "Comfort Inn," it confirms the update.
        /// Otherwise, it returns a message indicating that the hotel was not found.
        /// The response is returned as a JsonResult with an optional status code.
        /// </remarks>
        [HttpPut("edit")]
        public IActionResult EditBookRequestJson([FromBody] HotelUpdateRequest request)
        {
            return EditBookRequest(request, _logger);
        }

        /// <summary>
        /// Updates hotel information based on the provided update request.
        /// </summary>
        /// <returns>
        /// A JsonResult containing a message indicating whether the hotel information was updated or not found.
        /// </returns>
        /// <remarks>
        /// This action method updates hotel information based on the provided HotelUpdateRequest.
        /// If the hotel name is "Comfort Inn," it confirms the update.
        /// Otherwise, it returns a message indicating that the hotel was not found.
        /// The response is returned as a JsonResult with an optional status code.
        /// </remarks>
        [HttpPut("edit/text"), Consumes(MediaTypeNames.Text.Plain)]
        public IActionResult EditBookRequestText()
        {
            // Read the request body as a string
            var input = Request.Body.ReadAsStringAsync().Result;

            // Extract hotel name and room type using regular expressions
            var request = new HotelUpdateRequest
            {
                HotelName = Regex.Match(input, pattern: "Luxury Hotel").Value
            };

            // Submit the hotel edit booking request and return the result
            return EditBookRequest(request, _logger);
        }

        /// <summary>
        /// Updates hotel information based on the provided update request.
        /// </summary>
        /// <param name="request">The hotel update request containing details such as the hotel name to update.</param>
        /// <returns>
        /// A JsonResult containing a message indicating whether the hotel information was updated or not found.
        /// </returns>
        /// <remarks>
        /// This action method updates hotel information based on the provided HotelUpdateRequest.
        /// If the hotel name is "Comfort Inn," it confirms the update.
        /// Otherwise, it returns a message indicating that the hotel was not found.
        /// The response is returned as a JsonResult with an optional status code.
        /// </remarks>
        [HttpPut("edit/encoded"), Consumes(MediaTypeNames.Application.FormUrlEncoded)]
        public IActionResult EditBookRequestUrlEncoded([FromForm] HotelUpdateRequest request)
        {
            return EditBookRequest(request, _logger);
        }

        /// <summary>
        /// Updates hotel information based on the provided update request.
        /// </summary>
        /// <param name="request">The hotel update request containing details such as the hotel name to update.</param>
        /// <returns>
        /// A JsonResult containing a message indicating whether the hotel information was updated or not found.
        /// </returns>
        /// <remarks>
        /// This action method updates hotel information based on the provided HotelUpdateRequest.
        /// If the hotel name is "Comfort Inn," it confirms the update.
        /// Otherwise, it returns a message indicating that the hotel was not found.
        /// The response is returned as a JsonResult with an optional status code.
        /// </remarks>
        [HttpPut("edit/xml"), Consumes(MediaTypeNames.Text.Xml)]
        public IActionResult EditBookRequestXml([FromBody] HotelUpdateRequest request)
        {
            return EditBookRequest(request, _logger);
        }

        // Updates hotel information based on the provided update request.
        private static IActionResult EditBookRequest(HotelUpdateRequest request, ILogger logger)
        {
            try
            {
                // Log the start of the hotel update action
                logger.LogInformation("Attempting hotel update for HotelName: {HotelName}", request.HotelName);

                // Check if the provided hotel name is "Luxury Hotel"
                if (request.HotelName == "Luxury Hotel")
                {
                    // If yes, create a response indicating the successful update
                    var response = new { Message = "Hotel information updated for Luxury Hotel" };

                    // Log the successful hotel update
                    logger.LogInformation("Hotel information updated successfully.");

                    // Return a JsonResult with the confirmation message
                    return new JsonResult(response);
                }
                else
                {
                    // If no, create a response indicating that the hotel was not found
                    var response = new { Message = "Hotel not found" };

                    // Log that the hotel was not found
                    logger.LogInformation("Hotel not found.");

                    // Return a JsonResult with the failure message and status code 404 (Not Found)
                    return new JsonResult(response)
                    {
                        StatusCode = 404
                    };
                }
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception during the hotel update
                logger.LogError("An error occurred during hotel update: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return new ObjectResult("An error occurred during the hotel booking.")
                {
                    StatusCode = 500
                };
            }
        }
        #endregion

        #region *** Utilities         ***
        // Generates sample hotel search results based on the specified parameters.
        // This method generates sample hotel search results for demonstration purposes.
        // The results include details such as hotel name, location, price per night, and rating.
        // The generated HotelSearchResults object is returned for further use.
        private static HotelSearchResults GetSampleSearchResults(string destination, int adults, int children)
        {
            var hotels = new List<HotelResult>
            {
                new()
                {
                    Name = "Luxury Hotel",
                    Location = "Downtown",
                    PricePerNight = 250,
                    Rating = 5
                },
                new()
                {
                    Name = "Comfort Inn",
                    Location = "Suburb",
                    PricePerNight = 120,
                    Rating = 4
                },
                new()
                {
                    Name = "Budget Lodge",
                    Location = "City Center",
                    PricePerNight = 80,
                    Rating = 3
                }
            };

            return new HotelSearchResults
            {
                Destination = destination,
                Adults = adults,
                Children = children,
                Hotels = hotels
            };
        }
        #endregion

        #region *** Models            ***
        /// <summary>
        /// Represents the results of a hotel search operation.
        /// </summary>
        public class HotelSearchResults
        {
            /// <summary>
            /// Gets or sets the destination for the hotel search.
            /// </summary>
            public string Destination { get; set; }

            /// <summary>
            /// Gets or sets the number of adults in the search party.
            /// </summary>
            public int Adults { get; set; }

            /// <summary>
            /// Gets or sets the number of children in the search party.
            /// </summary>
            public int Children { get; set; }

            /// <summary>
            /// Gets or sets the list of hotel results returned from the search.
            /// </summary>
            public List<HotelResult> Hotels { get; set; }
        }

        /// <summary>
        /// Represents information about a hotel obtained from a search result.
        /// </summary>
        public class HotelResult
        {
            /// <summary>
            /// Gets or sets the name of the hotel.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the location of the hotel.
            /// </summary>
            public string Location { get; set; }

            /// <summary>
            /// Gets or sets the price per night for the hotel.
            /// </summary>
            [XmlAttribute]
            public decimal PricePerNight { get; set; }

            /// <summary>
            /// Gets or sets the rating of the hotel.
            /// </summary>
            public int Rating { get; set; }
        }

        /// <summary>
        /// Represents a request for booking a hotel with specific details.
        /// </summary>
        public class HotelBookingRequest
        {
            /// <summary>
            /// Gets or sets the name of the hotel to be booked.
            /// </summary>
            public string HotelName { get; set; }

            /// <summary>
            /// Gets or sets the type of room to be booked.
            /// </summary>
            public string RoomType { get; set; }

            /// <summary>
            /// Gets or sets the rating of the room to be booked.
            /// </summary>
            public double Rating { get; set; }
        }

        /// <summary>
        /// Represents a request to update information about a hotel.
        /// </summary>
        public class HotelUpdateRequest
        {
            /// <summary>
            /// Gets or sets the name of the hotel for which information is to be updated.
            /// </summary>
            public string HotelName { get; set; }

            /// <summary>
            /// Gets or sets the new information to be associated with the hotel.
            /// </summary>
            public string NewInfo { get; set; }
        }

        /// <summary>
        /// Represents a request to post a review for a hotel.
        /// </summary>
        public class HotelReviewRequest
        {
            /// <summary>
            /// Gets or sets the name of the hotel for which the review is being posted.
            /// </summary>
            public string HotelName { get; set; }

            /// <summary>
            /// Gets or sets the review content.
            /// </summary>
            public string Review { get; set; }
        }

        /// <summary>
        /// Represents a request for user login with specified credentials.
        /// </summary>
        public class LoginRequest
        {
            /// <summary>
            /// Gets or sets the username for the login request.
            /// </summary>
            public string Username { get; set; }

            /// <summary>
            /// Gets or sets the password for the login request.
            /// </summary>
            public string Password { get; set; }
        }

        /// <summary>
        /// Represents a response from a service, containing a message.
        /// </summary>
        public class ServiceResponse
        {
            /// <summary>
            /// Gets or sets the message contained in the service response.
            /// </summary>
            public string Message { get; set; }
        }
        #endregion
    }

    /// <summary>
    /// Controller for handling test-related API requests in an external G4™ repository mock,
    /// facilitating unit testing for an external repository.
    /// </summary>
    /// <remarks>Initializes a new instance of the <see cref="TestController"/> class.</remarks>
    /// <param name="logger">Logger instance for logging.</param>
    [ApiController]
    [Route("api/v1/g4")]
    public class TestController(ILogger<TestController> logger) : ControllerBase
    {
        // Represents the string comparison method used throughout the controller.
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        // Options for configuring JSON serialization.
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // Logger instance for logging within the <see cref="TestController"/>.
        private readonly ILogger<TestController> _logger = logger;

        // Collection of predefined plugin attributes for test actions.
        private static readonly IEnumerable<G4PluginAttribute> s_plugins =
        [
            new G4PluginAttribute
            {
                Key = "ExternalTestAction",
                PluginType = "Action"
            },
            new G4PluginAttribute
            {
                Key = "ExternalDemoAction",
                PluginType = "Action"
            },
            new G4PluginAttribute
            {
                Key = "ExternalAssertion",
                PluginType = "Assertion"
            },
            new G4PluginAttribute
            {
                Key = "Between",
                PluginType = "Operator"
            }
        ];

        #region *** Controller ***
        /// <summary>
        /// Endpoint for testing the availability of the external G4™ repository mock.
        /// </summary>
        /// <returns>An <see cref="IActionResult"/> indicating the result of the ping operation.</returns>
        [HttpGet, Route("ping")]
        public IActionResult Ping()
        {
            // Retrieve host and port information
            var host = Request.Host.Host;
            var port = Request.Host.Port;

            // Log an informational message
            _logger.LogInformation("Ping request received from {Host}:{Port}.", host, port);

            // Return "pong" as the response
            return Ok("pong");
        }

        /// <summary>
        /// Endpoint for retrieving the available test actions in the external G4™ repository mock.
        /// </summary>
        /// <remarks>This action returns a list of predefined test actions available in the external G4™ repository mock.</remarks>
        /// <returns>An <see cref="IActionResult"/> containing the list of available test actions.</returns>
        [HttpGet, Route("plugins/action")]
        public IActionResult GetActions()
        {
            // Retrieve host and port information
            var host = Request.Host.Host;
            var port = Request.Host.Port;

            // Log an informational message
            _logger.LogInformation("Request to get available actions received from {Host}:{Port}.", host, port);

            // Return the list of available test actions
            return Ok(s_plugins);
        }

        /// <summary>
        /// Endpoint for retrieving information about a specific test action in the external G4™ repository mock.
        /// </summary>
        /// <remarks>This method returns detailed information about the specified test action based on its unique name.</remarks>
        /// <param name="name">The name of the test action.</param>
        /// <returns>An <see cref="IActionResult"/> containing information about the specified test action.</returns>
        [HttpGet, Route("plugins/action/{name}")]
        public IActionResult GetAction([FromRoute] string name)
        {
            try
            {
                // Log the start of the GetAction endpoint
                _logger.LogInformation("Attempting to retrieve information for test action: {name}", name);

                // Retrieve information about the specified test action
                var selectedAction = s_plugins.FirstOrDefault(i => i.Key.Equals(name, Compare));

                if (selectedAction != null)
                {
                    // Log the successful retrieval of information
                    _logger.LogInformation("Information retrieved successfully for test action: {name}", name);

                    // Return information about the specified test action
                    return Ok(selectedAction);
                }
                else
                {
                    // Log that the specified test action was not found
                    _logger.LogInformation("Test action not found: {name}", name);

                    // Return a NotFound response with a message
                    return NotFound($"Test action not found: {name}");
                }
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception
                _logger.LogError("An error occurred during GetAction: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return StatusCode(500, "An error occurred during GetAction.");
            }
        }

        /// <summary>
        /// Endpoint for invoking a test action in the external G4™ repository mock.
        /// </summary>
        /// <remarks>
        /// This method simulates the invocation of a test action and returns a response containing various
        /// parameters, entities, exceptions, extractions, and session parameters.
        /// </remarks>
        /// <param name="request">The <see cref="PluginRequestModel"/> representing the request for the test action.</param>
        /// <returns>An <see cref="IActionResult"/> containing the response from the invoked test action.</returns>
        [HttpPost, Route("plugins/action/invoke")]
        public IActionResult InvokeAction([FromBody] PluginRequestModel request)
        {
            try
            {
                // Log the start of the InvokeAction endpoint
                _logger.LogInformation("Attempting to invoke test action with session {Session}.", request.Session);

                // Simulate the response from the invoked test action
                var applicationParams = new Dictionary<string, ApplicationParametersModel>
                {
                    ["ApplicationParameter"] = new()// "CreatedByExternalAction"
                };
                var entity = new Dictionary<string, object>
                {
                    ["Entity"] = false
                };
                var sessionParams = new Dictionary<string, object>
                {
                    ["SessionParameter"] = "CreatedByExternalAction"
                };
                var response = new PluginResponseModel
                {
                    ApplicationParameters = applicationParams,
                    Entity = entity,
                    Exceptions = [],
                    Extractions = [],
                    SessionParameters = sessionParams
                };

                // Log the successful invocation of the test action
                _logger.LogInformation("Test action invoked successfully.");

                // Return the simulated response from the invoked test action
                return Ok(response);
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception
                _logger.LogError("An error occurred during InvokeAction: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return StatusCode(500, "An error occurred during InvokeAction.");
            }
        }

        /// <summary>
        /// Endpoint for retrieving available assertion methods in the external G4™ repository mock.
        /// </summary>
        /// <remarks>This action returns a list of predefined assertion methods available in the external G4™ repository mock.</remarks>
        /// <returns>An <see cref="IActionResult"/> containing the list of available assertion methods.</returns>
        [HttpGet, Route("plugins/assertion")]
        public IActionResult GetAssertMethods()
        {
            try
            {
                // Retrieve host and port information
                var host = Request.Host.Host;
                var port = Request.Host.Port;

                // Log the start of the GetAssertMethods endpoint
                _logger.LogInformation("Attempting to retrieve available assertion methods.");

                // Log an informational message with host and port details
                _logger.LogInformation("Request to get available assertion methods received from {host}:{port}.", host, port);

                // Return the list of available assertion methods
                return Ok(new[]
                {
                    new G4PluginAttribute
                    {
                        Key = "ExternalAssertion"
                    }
                });
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception
                _logger.LogError("An error occurred during GetAssertMethods: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return StatusCode(500, "An error occurred during GetAssertMethods.");
            }
        }

        /// <summary>
        /// Endpoint for retrieving information about a specific assertion method in the external G4™ repository mock.
        /// </summary>
        /// <remarks>This method returns detailed information about the specified assertion method based on its unique name.</remarks>
        /// <param name="name">The name of the assertion method.</param>
        /// <returns>An <see cref="IActionResult"/> containing information about the specified assertion method.</returns>
        [HttpGet, Route("plugins/assertion/{name}")]
        public IActionResult GetAssertMethod([FromRoute] string name)
        {
            try
            {
                // Log the start of the GetAssertMethod endpoint
                _logger.LogInformation("Attempting to retrieve information for assertion method: {name}", name);

                // Retrieve information about the specified assertion method
                var selectedAssertion = s_plugins.FirstOrDefault(i => i.Key.Equals(name, Compare));

                if (selectedAssertion != null)
                {
                    // Log the successful retrieval of information
                    _logger.LogInformation("Information retrieved successfully for assertion method: {name}", name);

                    // Return information about the specified assertion method
                    return Ok(selectedAssertion);
                }
                else
                {
                    // Log that the specified assertion method was not found
                    _logger.LogInformation("Assertion method not found: {name}", name);

                    // Return a NotFound response with a message
                    return NotFound($"Assertion method not found: {name}");
                }
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception
                _logger.LogError("An error occurred during GetAssertMethod: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return StatusCode(500, "An error occurred during GetAssertMethod.");
            }
        }

        /// <summary>
        /// Endpoint for invoking an assertion method in the external G4™ repository mock.
        /// </summary>
        /// <remarks>This action simulates the invocation of an assertion method and returns the result based on the provided request.</remarks>
        /// <param name="request">The <see cref="PluginRequestModel"/> containing information about the assertion method to invoke.</param>
        /// <returns>An <see cref="IActionResult"/> containing the result of the assertion method invocation.</returns>
        [HttpPost, Route("plugins/assertion/invoke")]
        public IActionResult InvokeAssertMethod([FromBody] PluginRequestModel request)
        {
            try
            {
                // Log the start of the InvokeAssertMethod endpoint
                _logger.LogInformation("Attempting to invoke assertion method.");

                // Deserialize the action rule model from the request entity
                var action = JsonSerializer.Deserialize<ActionRuleModel>($"{request.Entity}", s_jsonOptions);

                // Log information about the assertion being invoked
                _logger.LogInformation("Invoking assertion method '{Name}' with action rule: '{action}'", request.Name, action);

                // Create a content dictionary for the assertion
                var content = new Dictionary<string, object>
                {
                    [AssertionProperties.Actual] = action.OnAttribute.Equals("index", StringComparison.OrdinalIgnoreCase)
                        ? "0"
                        : "Foo Bar"
                };

                // Add any additional parameters to the content dictionary
                foreach (var argument in request.Arguments)
                {
                    content[argument.Key] = argument.Value;
                }

                // Create a G4EntityModel with the assertion content
                var entity = new G4EntityModel { Content = content };

                // Create a PluginResponseModel with the assertion result
                var response = new PluginResponseModel
                {
                    Entity = entity.Content
                };

                // Add a demo exception to the response if specified in the capabilities
                response.Exceptions = request.Capabilities.TryGetValue("exceptions", out object value) && bool.Parse($"{value}")
                    ? [new G4ExceptionModel { PluginName = request.Name, ReasonPhrase = "Demo Exception" }]
                    : response.Exceptions;

                // Log the successful invocation of the assertion method
                _logger.LogInformation("Assertion method invoked successfully.");

                // Return the result of the assertion method invocation
                return Ok(response);
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception
                _logger.LogError("An error occurred during InvokeAssertMethod: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return StatusCode(500, "An error occurred during InvokeAssertMethod.");
            }
        }

        /// <summary>
        /// Endpoint for retrieving a list of operators available in the external G4™ repository mock.
        /// </summary>
        /// <remarks>This action returns a collection of <see cref="G4PluginAttribute"/> instances representing operators.</remarks>
        /// <returns>An <see cref="IActionResult"/> containing the list of operators.</returns>
        [HttpGet, Route("plugins/operator")]
        public IActionResult GetOperators()
        {
            try
            {
                // Retrieve host and port information
                var host = Request.Host.Host;
                var port = Request.Host.Port;

                // Log the start of the GetOperators endpoint
                _logger.LogInformation("Attempting to retrieve the list of operators.");

                // Log an informational message with host and port details
                _logger.LogInformation("Request to get operators received from {host}:{port}.", host, port);

                // Create the list of operators response
                var response = new[]
                {
                    new G4PluginAttribute
                    {
                        Key = "ExternalOperator",
                        PluginType = "Operator"
                    },
                    new G4PluginAttribute
                    {
                        Key = "Between",
                        PluginType = "Operator"
                    },
                    new G4PluginAttribute
                    {
                        Key = "NoType"
                    }
                };

                // Log the successful completion of the GetOperators endpoint
                _logger.LogInformation("List of operators retrieved successfully.");

                // Return the list of operators
                return Ok(response);
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception
                _logger.LogError("An error occurred during GetOperators: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return StatusCode(500, "An error occurred during GetOperators.");
            }
        }

        /// <summary>
        /// Endpoint for retrieving details of a specific operator from the external G4™ repository mock.
        /// </summary>
        /// <remarks>This method returns detailed information about the specified operator method based on its unique name.</remarks>
        /// <param name="name">The name of the operator to retrieve.</param>
        /// <returns>An <see cref="IActionResult"/> containing the details of the specified operator.</returns>
        [HttpGet, Route("plugins/operator/{name}")]
        public IActionResult GetOperator([FromRoute] string name)
        {
            try
            {
                // Log the start of the GetOperator endpoint
                _logger.LogInformation("Attempting to retrieve details for operator: {name}", name);

                // Return details of the specified operator
                var selectedOperator = s_plugins.FirstOrDefault(i => i.Key.Equals(name, Compare));

                if (selectedOperator != null)
                {
                    // Log the successful retrieval of operator details
                    _logger.LogInformation("Details retrieved successfully for operator: {name}", name);

                    // Return details of the specified operator
                    return Ok(selectedOperator);
                }
                else
                {
                    // Log that the specified operator was not found
                    _logger.LogInformation("Operator not found: {name}", name);

                    // Return a NotFound response with a message
                    return NotFound($"Operator not found: {name}");
                }
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception
                _logger.LogError("An error occurred during GetOperator: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return StatusCode(500, "An error occurred during GetOperator.");
            }
        }

        /// <summary>
        /// Endpoint for invoking an operator from the external G4™ repository mock.
        /// </summary>
        /// <remarks>
        /// This method simulates the invocation of an operator and returns a response based on the provided capabilities
        /// and the operator's behavior. It deserializes the action rule model from the request entity, sets up a content
        /// dictionary with operator-related information, creates instances of G4EntityModel and G4ExtractionModel,
        /// and returns a PluginResponseModel.
        /// </remarks>
        /// <param name="request">The request model containing information about the operator to invoke.</param>
        /// <returns>An <see cref="IActionResult"/> containing the response from invoking the specified operator.</returns>
        [HttpPost, Route("plugins/operator/invoke")]
        public IActionResult InvokeOperator([FromBody] PluginRequestModel request)
        {
            try
            {
                // Log the start of the InvokeOperator endpoint
                _logger.LogInformation("Attempting to invoke operator: {Name}", request.Name);

                // Deserialize the action rule model from the request entity
                var action = JsonSerializer.Deserialize<ActionRuleModel>($"{request.Entity}", s_jsonOptions);

                // Set up content dictionary with operator-related information
                var content = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    [AssertionProperties.Evaluation] = request.Capabilities?.ContainsKey("actual") != true
                        ? "false"
                        : request.Capabilities["actual"],
                    [AssertionProperties.Expected] = "PassMessage",
                    [AssertionProperties.Actual] = "PassMessage",
                    [AssertionProperties.Operator] = action.PluginName
                };

                // Create G4EntityModel and G4ExtractionModel instances
                var entity = new G4EntityModel { Content = content };
                var extraction = new G4ExtractionModel { Entities = [entity] };
                var extractions = new[] { extraction };

                // Create the response model
                var response = new PluginResponseModel { Entity = content, Extractions = extractions };

                // Handle exceptions based on the capabilities
                response.Exceptions = request.Capabilities.TryGetValue("exceptions", out object value) && bool.Parse($"{value}")
                    ? [new G4ExceptionModel { PluginName = request.Name, ReasonPhrase = "Demo Exception" }]
                    : response.Exceptions;

                // Log the successful completion of the InvokeOperator endpoint
                _logger.LogInformation("Operator invoked successfully: {Name}", request.Name);

                // Return the response
                return Ok(response);
            }
            catch (Exception e)
            {
                // Log an error for any unexpected exception
                _logger.LogError("An error occurred during InvokeOperator: {Message}", e.Message);

                // Return a StatusCode 500 response with an error message
                return StatusCode(500, "An error occurred during InvokeOperator.");
            }
        }

        /// <summary>
        /// Retrieves the list of plugin types.
        /// </summary>
        /// <returns>An IActionResult containing the list of plugin types.</returns>
        [HttpGet, Route("types")]
        public IActionResult GetTypes()
        {
            // Select and return the PluginType of each plugin in s_plugins
            return Ok(s_plugins.Select(i => i.PluginType));
        }

        /// <summary>
        /// Retrieves the list of plugin.
        /// </summary>
        /// <returns>An IActionResult containing the list of plugin.</returns>
        [HttpGet, Route("plugins")]
        public IActionResult GetPlugins()
        {
            return Ok(s_plugins);
        }
        #endregion
    }

    /// <summary>
    /// Represents the startup configuration for the application.
    /// </summary>
    /// <remarks>Initializes a new instance of the <see cref="Startup"/> class.</remarks>
    /// <param name="configuration">The configuration for the application.</param>
    public class Startup(IConfiguration configuration)
    {
        /// <summary>
        /// Gets the configuration for the application.
        /// </summary>
        public IConfiguration Configuration { get; } = configuration;

        /// <summary>
        /// Configures services for the application.
        /// </summary>
        /// <param name="services">The collection of services to configure.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            // Configure JSON options for controllers
            services.AddControllers().AddJsonOptions(i =>
            {
                i.JsonSerializerOptions.WriteIndented = true;
                i.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                i.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                i.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                i.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });

            // Enable XML serialization for controllers
            services.AddControllers().AddXmlSerializerFormatters();

            // Add Swagger generation services to the service collection.
            services.AddSwaggerGen();
        }

        /// <summary>
        /// Configures the application.
        /// </summary>
        /// <param name="app">The application builder.</param>
        public void Configure(IApplicationBuilder app)
        {
            // Enable Developer Exception Page for detailed error information during development
            app.UseDeveloperExceptionPage();

            // Enable routing
            app.UseRouting();

            // Enable authorization
            app.UseAuthorization();

            // Map controllers for handling endpoints
            app.UseEndpoints(endpoints => endpoints.MapControllers());

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI();

            // Configure Swagger UI
            app.UseSwaggerUI(options =>
            {
                // Set the Swagger JSON endpoint and API version
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");

                // Set the route prefix for the Swagger UI
                options.RoutePrefix = string.Empty;
            });
        }
    }

    /// <summary>
    /// Represents a simple web server for hosting a web application.
    /// </summary>
    public static class WebServer
    {
        // Static variable to hold the web host instance.
        private static IWebHost s_webHost = NewWebHost(port: 9002, shutdownTimeout: TimeSpan.FromSeconds(60));

        /// <summary>
        /// Starts the web host asynchronously.
        /// </summary>
        public static void StartWebHost() => Task.Run(s_webHost.Run);

        /// <summary>
        /// Creates a new web host with default settings.
        /// </summary>
        public static void NewWebHost()
            => s_webHost = NewWebHost(port: 9002, shutdownTimeout: TimeSpan.FromSeconds(60));

        // Creates a new web host with the specified port and shutdown timeout.
        private static IWebHost NewWebHost(int port, TimeSpan shutdownTimeout)
        {
            return WebHost
                .CreateDefaultBuilder()
                .UseUrls()
                .ConfigureKestrel(i => i.Listen(IPAddress.Any, port))
                .UseStartup<Startup>()
                .UseShutdownTimeout(shutdownTimeout)
                .Build();
        }

        /// <summary>
        /// Removes the existing web host, stopping and disposing it if it exists.
        /// </summary>
        public static void RemoveWebHost()
        {
            try
            {
                // Attempt to stop and dispose the web host
                s_webHost?.StopAsync().GetAwaiter().GetResult();
                s_webHost?.Dispose();
            }
            catch (Exception e)
            {
                // Handle any exceptions that might occur during stopping or disposing
                Console.WriteLine($"{e}");
                s_webHost.Dispose();
            }
        }
    }
}
