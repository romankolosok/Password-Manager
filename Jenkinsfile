pipeline {
    agent any

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        PROJECT_PATH = 'PasswordManager.sln'
        CORE_PROJECT_PATH = 'PasswordManager.Core/PasswordManager.Core.csproj'
        TESTS_PROJECT_PATH = 'PasswordManager.Tests/PasswordManager.Tests.csproj'
        APP_PROJECT_PATH = 'PasswordManager.App/PasswordManager.App.csproj'
        GITHUB_TOKEN = credentials('github-token-id')
        REPO_OWNER = 'romankolosok'
        REPO_NAME = 'PasswordManager'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        // Restore full solution; build only Core + Tests (Linux-friendly).
        stage('Restore & Build') {
            steps {
                sh "dotnet restore ${env.PROJECT_PATH}"
                sh "dotnet build ${env.CORE_PROJECT_PATH} --configuration Release --no-restore"
                sh "dotnet build ${env.TESTS_PROJECT_PATH} --configuration Release --no-restore"
            }
        }

        // Test Core library only. coverlet.msbuild: collect Cobertura coverage for reporting (no threshold enforcement yet).
        stage('Test & Coverage') {
            steps {
                sh "dotnet test ${env.TESTS_PROJECT_PATH} --configuration Release --no-build --logger 'junit;LogFilePath=test-results.xml' /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=coverage.cobertura.xml"
            }
            post {
                always {
                    junit allowEmptyResults: true, testResults: '**/test-results.xml'
                    recordCoverage(
                        tools: [[parser: 'COBERTURA', pattern: '**/coverage.cobertura.xml']],
                        id: 'dotnet-coverage',
                        name: '.NET Coverage'
                    )
                }
            }
        }

        // Build and release the app. Requires PUBLISH_RELEASE=true. Runs on current agent; on Linux the stage is skipped (WPF needs Windows). Add a Windows agent with label 'windows' and use it for this stage to publish.
        stage('Publish & Release') {
            when {
                allOf {
                    anyOf { branch 'master'; branch 'main' }
                    not { changeRequest() }
                    expression { return env.PUBLISH_RELEASE == 'true' }
                }
            }
            steps {
                script {
                    if (env.NODE_NAME?.contains('windows') || !isUnix()) {
                        echo "Deploying release from branch: ${env.BRANCH_NAME}"
                        checkout scm
                        bat "dotnet restore ${env.PROJECT_PATH}"
                        bat "dotnet publish ${env.APP_PROJECT_PATH} --configuration Release -o publish"
                        powershell "Compress-Archive -Path .\\publish\\* -DestinationPath password-manager-release.zip -Force"
                        def tag = "v1.0.${env.BUILD_NUMBER}"
                        powershell """
                            \$body = @{ tag_name = '${tag}'; target_commitish = '${env.BRANCH_NAME}'; name = '${tag}'; body = 'Automated release'; draft = \$false; prerelease = \$false } | ConvertTo-Json
                            \$r = Invoke-RestMethod -Uri 'https://api.github.com/repos/${env.REPO_OWNER}/${env.REPO_NAME}/releases' -Method Post -Headers @{ Authorization = "token ${env.GITHUB_TOKEN}"; 'Content-Type' = 'application/json' } -Body \$body
                            \$r.id | Out-File -FilePath release_id.txt -NoNewline
                        """
                        def releaseId = readFile('release_id.txt').trim()
                        powershell """
                            \$uri = "https://uploads.github.com/repos/${env.REPO_OWNER}/${env.REPO_NAME}/releases/${releaseId}/assets?name=password-manager-release.zip"
                            Invoke-RestMethod -Uri \$uri -Method Post -Headers @{ Authorization = "token ${env.GITHUB_TOKEN}"; 'Content-Type' = 'application/zip' } -InFile 'password-manager-release.zip'
                        """
                    } else {
                        echo "Skipping app publish: WPF requires Windows. Set PUBLISH_RELEASE=true and run this job on a Windows agent (label 'windows') to publish."
                    }
                }
            }
        }
    }

    post {
        always {
            step([$class: 'GitHubCommitStatusSetter', contextSource: [$class: 'DefaultCommitContextSource']])
            cleanWs()
        }
    }
}
