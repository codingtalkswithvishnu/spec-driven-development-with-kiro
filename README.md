# Spec-driven development with Kiro — companion examples

One fictional patient-consent application, developed in small stages. This
repository contains code, fixtures, and lab instructions only. The book and its
cover are maintained separately.

## About the book

**Amazon Kiro: Spec-Driven Development**

*Building Reliable Software with AI Workflows*

**Author: Vishnu VG**

[Read the book on Amazon](https://www.amazon.in/dp/B0HPGY8B44)

The book explains how to use Kiro to turn an idea into reviewed requirements,
a design, implementation tasks, and checked software. The companion follows one
fictional healthcare patient-consent application from local behavior to AWS
persistence, authorization, asynchronous work, testing, and team review.

Use the chapter-to-folder map below while reading. These examples accompany
the revised learning path; an earlier published edition may use different
headings or ordering. Follow each folder's README for its current starting point
and validation limits. The manuscript, cover, and ebook files are not included.

## About the author

Vishnu VG is an enterprise architect and software engineer with over 18 years
of experience in cloud systems and distributed applications. His work includes
mission-critical platforms, AWS and Microsoft Azure backends, and practical AI-assisted engineering.
He is an AWS Community Builder and the creator of *Coding Talks with Vishnu VG*.

- [LinkedIn](https://www.linkedin.com/in/vishnuvgtvm/)
- [AWS Builder profile](https://builder.aws.com/community/@vishnuvgtvm?tab=badges)

## Understand the application first

Read [the project overview](PROJECT_OVERVIEW.md). P-001 owns consent C-001 for
research-demo. A withdrawal must check ownership, preserve other purposes, avoid
another state transition on repeat, and keep its required records consistent.
The cloud stage adds trusted application identity and asynchronous delivery.

## Follow the learning path

| Folder | Book chapters | What you do |
| --- | --- | --- |
| [00-start-here](00-start-here/) | 1–2, 4 | Prepare the brief, stable contracts, and lifecycle notes |
| [01-local-consent](01-local-consent/) | 3 | Run the completed local reference and its 12 checks |
| [02-project-context](02-project-context/) | 5, 7 | Add steering and a bounded verification hook |
| [03-mcp-context](03-mcp-context/) | 6 | Retrieve fictional issue and design context through a local MCP server |
| [04-git-workflow](04-git-workflow/) | 8–9 | Preserve a checkpoint and separate work with branches/worktrees |
| [05-aws-consent](05-aws-consent/) | 10 | Build and synthesize the protected API and event-processing infrastructure |
| [06-testing-and-review](06-testing-and-review/) | 11–12 | Run checks, examine failures, and prepare a reviewed delivery |

## Choose your learning route

Clone the repository, then enter its root folder:

```text
git clone https://github.com/codingtalkswithvishnu/spec-driven-development-with-kiro.git
cd spec-driven-development-with-kiro
```

**Build your own:** install .NET 10 and Node.js, then run from this repository root:

```text
node tools/create-practice.mjs ../consent-workshop
```

Choose an empty destination, or your Chapter 1 folder containing only the brief
and learning notes; these notes are preserved. The tool refuses existing code. Open that
separate folder in Kiro. The starter defines public contracts and acceptance
checks but deliberately leaves the implementation unfinished. Its checks should
fail until you implement the rules. See [contracts](00-start-here/contracts.md).

**Run the reference:** use folder 01 directly. It is completed sample code, not
evidence that your agent generated or tested your practice implementation.

Generated private code can vary. Keep the public boundary used by the checks,
verify the required behavior, and explain any deliberate contract change. Before
the cloud lab, use the provided cloud checkpoint; this prevents local design
differences from becoming an unexplained deployment problem.

## Requirements and verification

Use the current patched .NET 10 SDK. It is the LTS baseline selected for this
example and has an AWS Lambda managed runtime. See [Microsoft support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
and [Lambda C# runtimes](https://docs.aws.amazon.com/lambda/latest/dg/lambda-csharp.html).
Node.js is needed for the starter, MCP fixture, checks, and CDK; use a version
supported by the [CDK prerequisites](https://docs.aws.amazon.com/cdk/v2/guide/prerequisites.html).

See [VALIDATION.md](VALIDATION.md) for exactly what was executed. Compilation and
CDK synthesis do not prove a live cloud deployment works. AWS exercises create
billable resources only if you explicitly deploy them. Use fictional data and
an appropriate development account. Each folder explains cleanup.
