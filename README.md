# Warmup

Warmup is a server for warming up an IP address for sending emails, without
getting throttled, blocked or blacklisted.

## Docker images

A pre-built Docker image is available from Docker Hub:

```sh
docker run --rm --name warmup -p 8080:80 juselius/warmup
```

To build a Docker image from source:

```sh
docker build -t warmup .
```

## Install pre-requisites for developing

You'll need to install the following pre-requisites in order to build [SAFE](https://safe-stack.github.io/) applications.

* The [.NET Core SDK](https://www.microsoft.com/net/download)
* [FAKE 5](https://fake.build/) installed as a [global tool](https://fake.build/fake-gettingstarted.html#Install-FAKE)
* The [Yarn](https://yarnpkg.com/lang/en/docs/install/) package manager (you an also use `npm` but the usage of `yarn` is encouraged).
* [Node LTS](https://nodejs.org/en/download/) installed for the front end components.
* If you're running on OSX or Linux, you'll also need to install [Mono](https://www.mono-project.com/docs/getting-started/install/).

## Work with the application

To concurrently run the server and the client components in watch mode use the following command:

```bash
fake build -t Run
```

## Troubleshooting

* **fake not found** - If you fail to execute `fake` from command line after installing it as a global tool, you might need to add it to your `PATH` manually: (e.g. `export PATH="$HOME/.dotnet/tools:$PATH"` on unix) - [related GitHub issue](https://github.com/dotnet/cli/issues/9321)
