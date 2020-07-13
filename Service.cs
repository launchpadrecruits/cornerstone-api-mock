using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace csapi
{
    public enum InterviewType
    {
        LiveVideo,
        OnDemand
    }

    public class Reviewer
    {
        public string ReviewerEmail { get; set; }
        public string ReviewerName { get; set; }
    }

    public class CallbackDetails
    {
        public string CallbackUrl { get; set; }
    }

    public class AssignApplicantRequest
    {
        public InterviewType InterviewType { get; set; }
        public string InterviewId { get; set; }
        public string ApplicantFirstName { get; set; }
        public string ApplicantLastName { get; set; }
        public string ApplicantEmail { get; set; }
        public string PrimaryOwnerEmail { get; set; }
        public string RecruiterEmail { get; set; }
        public string JobRequisitionId { get; set; }
        public List<Reviewer> Reviewers { get; set; }
        public DateTime InterviewStartDate { get; set; }
        public DateTime InterviewEndDate { get; set; }
        public CallbackDetails CallbackData { get; set; }
    }

    public class AssignApplicantResponse
    {
        public string InterviewUrl { get; set; }
        public string RecruiterUrl { get; set; }
    }

    [ServiceContract]
    public interface IService
    {
        [OperationContract]
        string GetVersion();

        [OperationContract]
        AssignApplicantResponse AssignApplicant(AssignApplicantRequest req);
    }

    public class Service : IService
    {
        public string GetVersion()
        {
            return "0.0.1";
        }

        public AssignApplicantResponse AssignApplicant(AssignApplicantRequest req)
        {
            return new AssignApplicantResponse();
        }
    }
}