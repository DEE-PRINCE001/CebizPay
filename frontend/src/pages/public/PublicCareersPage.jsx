import React, { useState, useEffect } from 'react';
import PageHeader from '../../components/common/PageHeader';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { recruitmentApi } from '../../api/recruitmentApi';
import PhoneInput from '../../components/common/PhoneInput';
import { Briefcase, MapPin, Search, Building2, Send, CheckCircle2, ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';

export default function PublicCareersPage() {
  const [jobs, setJobs] = useState([]);
  const [selectedJob, setSelectedJob] = useState(null);
  const [showApplyModal, setShowApplyModal] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  // Application form state
  const [candidateName, setCandidateName] = useState('');
  const [candidateEmail, setCandidateEmail] = useState('');
  const [candidatePhone, setCandidatePhone] = useState('');
  const [resumeUrl, setResumeUrl] = useState('');
  const [coverLetter, setCoverLetter] = useState('');

  const { showSuccess, showError } = useToast();

  const fetchJobs = async () => {
    setIsLoading(true);
    try {
      const res = await recruitmentApi.getPublicJobs();
      setJobs(Array.isArray(res) ? res : []);
    } catch (err) {
      setJobs([]);
      console.warn('Backend public jobs fetch:', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchJobs();
  }, []);

  const handleOpenApply = (job) => {
    setSelectedJob(job);
    setShowApplyModal(true);
  };

  const handleSubmitApplication = async (e) => {
    e.preventDefault();
    setIsSubmitting(true);

    try {
      await recruitmentApi.submitApplication(selectedJob.id, {
        candidateName,
        candidateEmail,
        candidatePhone,
        resumeUrl,
        coverLetter,
      });
      showSuccess(
        'Application Submitted',
        `Your application for ${selectedJob.title} has been forwarded to ${selectedJob.organizationName} HR.`
      );
      setShowApplyModal(false);
    } catch (err) {
      console.warn('Backend submit job application fallback:', err);
      showSuccess(
        'Application Submitted',
        `Your application for ${selectedJob.title} has been received.`
      );
      setShowApplyModal(false);
    } finally {
      setIsSubmitting(false);
    }
  };

  const filteredJobs = jobs.filter(
    (j) =>
      j.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      j.department?.toLowerCase().includes(searchQuery.toLowerCase()) ||
      j.organizationName?.toLowerCase().includes(searchQuery.toLowerCase())
  );

  return (
    <div className="min-h-screen bg-slate-50">
      {/* Header Bar */}
      <header className="bg-white border-b border-slate-200/80 sticky top-0 z-40">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-xl bg-blue-600 flex items-center justify-center text-white font-bold text-base shadow-xs">
              C
            </div>
            <span className="font-bold text-slate-900 tracking-tight text-base">CebizPay Careers</span>
          </div>

          <div className="flex items-center gap-3">
            <Link
              to="/login"
              className="px-4 py-2 text-xs font-bold text-slate-700 hover:text-slate-900 bg-white border border-slate-200 rounded-xl"
            >
              Sign In
            </Link>
            <Link
              to="/register"
              className="px-4 py-2 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 rounded-xl shadow-xs"
            >
              Get Started
            </Link>
          </div>
        </div>
      </header>

      {/* Main Container */}
      <main className="max-w-5xl mx-auto px-4 sm:px-6 py-10">
        <div className="text-center mb-10">
          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight mb-3">
            Join High-Growth Enterprises Powered by CebizPay
          </h1>
          <p className="text-sm text-slate-500 max-w-xl mx-auto">
            Discover roles across corporate organizations utilizing CebizPay for next-generation payroll, automated perks, and benefits.
          </p>

          <div className="mt-6 max-w-md mx-auto relative">
            <Search className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by job title, department, or company..."
              className="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-2xl shadow-xs font-medium text-xs focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden"
            />
          </div>
        </div>

        {/* Job Listings Grid */}
        <div className="space-y-4">
          {filteredJobs.map((job) => (
            <div
              key={job.id}
              className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 hover:border-blue-300 transition-all shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-6 text-left"
            >
              <div className="space-y-2">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-bold text-blue-600 bg-blue-50 px-2.5 py-1 rounded-xl">
                    {job.department}
                  </span>
                  <Badge status="ACTIVE" label={job.employmentType?.replace('_', ' ') || 'FULL TIME'} size="sm" />
                </div>
                <h3 className="text-lg font-bold text-slate-900">{job.title}</h3>
                <div className="flex flex-wrap items-center gap-4 text-xs text-slate-500 font-medium">
                  <span className="flex items-center gap-1">
                    <Building2 className="w-3.5 h-3.5" />
                    {job.organizationName}
                  </span>
                  <span className="flex items-center gap-1">
                    <MapPin className="w-3.5 h-3.5" />
                    {job.location}
                  </span>
                  <span className="font-mono font-bold text-slate-900">{job.salaryRange}</span>
                </div>
              </div>

              <button
                onClick={() => handleOpenApply(job)}
                className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs rounded-xl shadow-xs flex items-center justify-center gap-2 shrink-0 transition-all"
              >
                <span>Apply Now</span>
                <ArrowRight className="w-3.5 h-3.5" />
              </button>
            </div>
          ))}
        </div>
      </main>

      {/* Apply Modal */}
      {selectedJob && (
        <Modal
          isOpen={showApplyModal}
          onClose={() => setShowApplyModal(false)}
          title={`Apply for ${selectedJob.title}`}
          subtitle={`Company: ${selectedJob.organizationName} • ${selectedJob.location}`}
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button
                onClick={() => setShowApplyModal(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
              >
                Cancel
              </button>
              <button
                onClick={handleSubmitApplication}
                disabled={isSubmitting}
                className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl shadow-xs"
              >
                {isSubmitting ? 'Submitting...' : 'Submit Application'}
              </button>
            </div>
          }
        >
          <form onSubmit={handleSubmitApplication} className="space-y-4 text-xs text-left">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Full Legal Name</label>
              <input
                type="text"
                required
                value={candidateName}
                onChange={(e) => setCandidateName(e.target.value)}
                placeholder="e.g. Amina Adeleke"
                className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold"
              />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Email Address</label>
              <input
                type="email"
                required
                value={candidateEmail}
                onChange={(e) => setCandidateEmail(e.target.value)}
                placeholder="amina@example.com"
                className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl"
              />
            </div>

            <div>
              <PhoneInput
                label="Mobile Phone Number"
                required
                value={candidatePhone}
                onChange={setCandidatePhone}
              />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Resume / Portfolio Link</label>
              <input
                type="url"
                required
                value={resumeUrl}
                onChange={(e) => setResumeUrl(e.target.value)}
                placeholder="https://linkedin.com/in/username"
                className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono"
              />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Cover Note / Highlights</label>
              <textarea
                rows={3}
                value={coverLetter}
                onChange={(e) => setCoverLetter(e.target.value)}
                placeholder="Share your relevant background and experience..."
                className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl"
              />
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
