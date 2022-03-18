# Tye frequently asked questions

**Q: What makes Tye different from other microservices oriented tools?**

A: Three main ways:

- Tye is optimized for .NET - we have built-in knowledge of how .NET projects work, which we use to power most experiences.
- Tye's development features are oriented towards *local development* (avoid running in a container unless necessary).
- Tye aims to solve problems along the whole spectrum of development to CI/CD based deployment.

The development features of Tye are the most mature right now. We plan to add more recipes and capabilities for deployment in the coming months.

**Q: Is Tye a competitor to, or replacement for frameworks like [Dapr](https://dapr.io)?**

A: No. Tye is a developer tool that you can use alongside programming models and frameworks. We try to not to have opinions about how you structure code or design applications.

**Q: What does it mean that Tye is an experiment?**

A: Put another way, Tye is a public incubation project from Microsoft. We're trying to build tools that help developers, and doing this with open source is the best way for us to try new ideas. We're looking to grow a community of early adopters who want to contribute and share ideas.

Being an experiment also means that the future of what happens to Tye is not clear. Tye could live on as a product.. or the ideas could be folded in to other products.

**Q: Do I have to use Azure to use Tye?**

A: No. You can use `tye deploy` with any Kubernetes cluster. You can use `tye run` on any mainstream OS.

**Q: Will Tye support other deployment targets other than Kubernetes?**

A. We're definitely interested in supporting deployments to a variety of runtime environments. Tell us what you're interested in. 

We had to start somewhere... we started with Kubernetes because it's:

- Not cloud-provider specific
- Very powerful
- Difficult for newcomers (we want to try and simplify the process)
