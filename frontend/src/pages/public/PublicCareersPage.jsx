import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { Briefcase, MapPin, Search, ArrowRight, Upload, CheckCircle2 } from 'lucide-react';
import { Link } from 'react-router-dom';

export default function PublicCareersPage() {
  const [selectedJob, setSelectedJob] = useState(null);
  const [showApplyModal, setShowApplyModal] = useState(false);
  const { showSuccess } = useToast();

  const [candidateName, setCandidateName] = useState('');
  const [candidateEmail, setCandidateEmail] = useState('');
  const [candidatePhone, setCandidatePhone] = useState('');
  const [resumeUrl, setResumeUrl] = useState('https://storage.cebizpay.com/resumes/my-cv.pdf');
  const [coverLetter, setCoverLetter] = useState('');
  const [searchQuery, setSearchQuery] = useState('');

  const [jobs] = useState([
    {
      id: 'job-101',
      title: 'Senior Fintech Backend Engineer (C# / .NET 10)',
      company: 'Apex Global Technologies Ltd',
      department: 'Engineering',
      location: 'Lagos, Nigeria (Hybrid)',
      employmentType: 'Full-Time',
      minSalary: 1400000.0,
      maxSalary: 2200000.0,
      currency: 'NGN',
      description: 'We are seeking an experienced Backend Engineer to lead double-entry financial ledger architecture, asynchronous payment webhooks, and multi-tenant high availability infrastructure on .NET 10.'
    },
    {
      id: 'job-102',
      title: 'Product Operations & Reconciliation Specialist',
      company: 'Apex Global Technologies Ltd',
      department: 'Finance & Accounting',
      location: 'Victoria Island, Lagos',
      employmentType: 'Full-Time',
      minSalary: 800000.0,
      maxSalary: 1200000.0,
      currency: 'NGN',
      description: 'Manage NIP interbank clearing, provider gateway requeries, chargeback recovery disputes, and daily statutory accounting reconciliation.'
    },
    {
      id: 'job-103',
      title: 'Senior Frontend React / Tailwind Architect',
      company: 'Quantum Health Technologies Ltd',
      department: 'Design & Engineering',
      location: 'Remote (West Africa)',
      employmentType: 'Full-Time',
      minSalary: 1600000.0,
      maxSalary: 2400000.0,
      currency: 'NGN',
      description: 'Design and maintain high-performance enterprise SPAs, accessible component libraries, and clean real-time financial dashboards.'
    }
  ]);

  const filteredJobs = jobs.filter((j) =>
    j.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
    j.department.toLowerCase().includes(searchQuery.toLowerCase()) ||
    j.company.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const handleApply = (e) => {
    e.preventDefault();
    showSuccess(
      'Application Submitted',
      `Your application for "${selectedJob.title}" at ${selectedJob.company} has been received by their hiring team.`
    );
    setShowApplyModal(false);
    setCandidateName('');
    setCandidateEmail('');
    setCoverLetter('');
  };

  return (
    <div className="max-w-5xl mx-auto py-6 px-4">
      {/* Hero Banner */}
      <div className="bg-linear-to-br from-slate-900 via-slate-800 to-blue-950 text-white rounded-3xl p-8 sm:p-12 mb-8 shadow-xl text-center">
        <span className="text-xs font-bold text-blue-400 uppercase tracking-widest block mb-2">
          CebizPay Ecosystem Careers
        </span>
        <h1 className="text-3xl sm:text-5xl font-extrabold tracking-tight mb-4">
          Discover Opportunities in Leading Tech &amp; Fintech Orgs
        </h1>
        <p className="text-sm text-slate-300 max-w-2xl mx-auto mb-8 leading-relaxed">
          Browse verified corporate openings from companies powered by CebizPay automated payroll, employee health, and competitive compensation.
        </p>

        {/* Search Bar */}
        <div className="max-w-xl mx-auto relative text-slate-900">
          <Search className="w-5 h-5 text-slate-400 absolute left-4 top-1/2 -translate-y-1/2 pointer-events-none" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search by job title, department, or company..."
            className="w-full pl-12 pr-4 py-3.5 bg-white rounded-2xl shadow-lg border-0 focus:ring-4 focus:ring-blue-500/30 text-sm font-medium outline-hidden"
          />
        </div>
      </div>

      {/* Openings Grid */}
      <div className="space-y-4 text-left">
        <h3 className="text-base font-bold text-slate-900 mb-4">
          Active Job Openings ({filteredJobs.length})
        </h3>

        {filteredJobs.map((job) => (
          <div
            key={job.id}
            className="bg-white rounded-3xl border border-slate-200/80 p-6 shadow-xs hover:border-slate-300 transition-all flex flex-col md:flex-row md:items-center justify-between gap-6"
          >
            <div className="space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <h4 className="text-lg font-bold text-slate-900">{job.title}</h4>
                <Badge status="ACTIVE" label={job.employmentType} size="sm" />
              </div>
              <p className="text-xs font-semibold text-slate-700">
                {job.company} • <span className="text-slate-500">{job.department}</span>
              </p>
              <div className="flex items-center gap-4 text-xs text-slate-500">
                <span className="flex items-center gap-1">
                  <MapPin className="w-3.5 h-3.5 text-slate-400" />
                  {job.location}
                </span>
                <span className="font-mono font-bold text-slate-800">
                  {formatCurrency(job.minSalary)} – {formatCurrency(job.maxSalary)} / month
                </span>
              </div>
              <p className="text-xs text-slate-600 leading-relaxed max-w-2xl pt-1">
                {job.description}
              </p>
            </div>

            <button
              onClick={() => {
                setSelectedJob(job);
                setShowApplyModal(true);
              }}
              className="px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl text-xs shadow-xs transition-colors shrink-0 flex items-center gap-2"
            >
              <span>Apply Now</span>
              <ArrowRight className="w-4 h-4" />
            </button>
          </div>
        ))}
      </div>

      {/* Apply Modal */}
      {selectedJob && (
        <Modal
          isOpen={showApplyModal}
          onClose={() => setShowApplyModal(false)}
          title={`Apply for ${selectedJob.title}`}
          subtitle={`${selectedJob.company} • ${selectedJob.location}`}
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button onClick={() => setShowApplyModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
              <button onClick={handleApply} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Submit Application</button>
            </div>
          }
        >
          <form onSubmit={handleApply} className="space-y-4 text-xs text-left">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Full Legal Name</label>
              <input type="text" required value={candidateName} onChange={(e) => setCandidateName(e.target.value)} placeholder="e.g. Babatunde Adeleke" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold" />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">Email Address</label>
                <input type="email" required value={candidateEmail} onChange={(e) => setCandidateEmail(e.target.value)} placeholder="name@example.com" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
              </div>
              <div>
                <label className="block font-semibold text-slate-700 mb-1">Phone Number</label>
                <input type="tel" required value={candidatePhone} onChange={(e) => setCandidatePhone(e.target.value)} placeholder="08022334455" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono" />
              </div>
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Resume / CV Document URL</label>
              <input type="url" required value={resumeUrl} onChange={(e) => setResumeUrl(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono" />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Cover Note / Qualifications</label>
              <textarea rows={3} required value={coverLetter} onChange={(e) => setCoverLetter(e.target.value)} placeholder="Briefly highlight your relevant technical experience..." className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
